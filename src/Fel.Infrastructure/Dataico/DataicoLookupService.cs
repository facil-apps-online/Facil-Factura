using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Dataico
{
    public enum DataicoLookupKind
    {
        Invoice,
        SupportDoc,
        SupportDocAdjustment,
        Payroll,
        PayrollDeletion,
        PayrollReplacement
    }

    // Lo que el portal muestra de un documento consultado en Dataico. No lleva el PDF ni el XML: se descargan aparte.
    public sealed class DataicoLookupSummary
    {
        public string Number { get; init; } = string.Empty;
        public string? Uuid { get; init; }
        public string? Cufe { get; init; }
        public string? Status { get; init; }
        public string? IssueDate { get; init; }
        public string? Customer { get; init; }
        public string? Total { get; init; }
        public List<string> Messages { get; init; } = new();
        public bool HasXml { get; init; }
        public bool HasPdf { get; init; }
    }

    public sealed record DataicoLookupResult(int Status, string? Error, DataicoLookupSummary? Summary)
    {
        public bool Ok => Summary != null;
    }

    public sealed record DataicoFileResult(int Status, string? Error, byte[]? Bytes, string? ContentType, string? FileName)
    {
        public bool Ok => Bytes != null;
    }

    // Consulta de documentos ya emitidos en la cuenta de Dataico del cliente (factura, documento soporte y su nota de ajuste, nómina y sus notas
    // de eliminación y de reemplazo) y descarga de su XML y su representación gráfica. Solo lee: no guarda nada. Los archivos pasan por nuestro
    // servidor para no exponer el token de la cuenta. Las formas de respuesta se confirmaron contra el sandbox de Dataico.
    public class DataicoLookupService
    {
        private readonly IDataicoApiService _api;
        private readonly ICryptoService _crypto;

        public DataicoLookupService(IDataicoApiService api, ICryptoService crypto)
        {
            _api = api;
            _crypto = crypto;
        }

        // Prefijo y número: el número es el último bloque de dígitos ("DSP201" -> "DSP", "201"). Dataico pide los dos por separado en documento
        // soporte y en nómina.
        public static bool TrySplit(string input, out string prefix, out string number)
        {
            var m = Regex.Match((input ?? string.Empty).Trim(), @"^(.*?)(\d+)$");
            prefix = m.Success ? m.Groups[1].Value.Trim() : string.Empty;
            number = m.Success ? m.Groups[2].Value : string.Empty;
            return m.Success && prefix.Length > 0;
        }

        public async Task<DataicoLookupResult> LookupAsync(Client client, DataicoLookupKind kind, string? input)
        {
            var (call, error) = await FetchAsync(client, kind, input, includePdf: false);
            if (error != null) return new DataicoLookupResult(error.Value.Status, error.Value.Message, null);
            var parsed = Parse(kind, call!.RawResponse);
            if (parsed == null) return new DataicoLookupResult(404, $"Dataico no encontró el documento {input?.Trim()}.", null);
            return new DataicoLookupResult(200, null, parsed.Summary);
        }

        public async Task<DataicoFileResult> DownloadAsync(Client client, DataicoLookupKind kind, string? input, string? fileType)
        {
            var isXml = string.Equals(fileType, "xml", StringComparison.OrdinalIgnoreCase);
            if (!isXml && !string.Equals(fileType, "pdf", StringComparison.OrdinalIgnoreCase))
                return new DataicoFileResult(400, "El tipo debe ser xml o pdf.", null, null, null);

            var (call, error) = await FetchAsync(client, kind, input, includePdf: !isXml);
            if (error != null) return new DataicoFileResult(error.Value.Status, error.Value.Message, null, null, null);
            var parsed = Parse(kind, call!.RawResponse);
            if (parsed == null) return new DataicoFileResult(404, $"Dataico no encontró el documento {input?.Trim()}.", null, null, null);

            var label = isXml ? "XML" : "PDF";
            byte[]? bytes = null;

            // El archivo puede venir como dirección (factura, documento soporte) o dentro de la respuesta (nómina).
            var url = isXml ? parsed.XmlUrl : parsed.PdfUrl;
            if (string.IsNullOrEmpty(url) && isXml && kind == DataicoLookupKind.Invoice && !string.IsNullOrEmpty(parsed.Summary.Uuid))
                url = $"https://api.dataico.com/direct/serve-file?invoices-type=invoice&document-id={Uri.EscapeDataString(parsed.Summary.Uuid)}&xml-type=invoice";
            if (!string.IsNullOrEmpty(url))
            {
                var file = await _api.DownloadFileAsync(url, DataicoDocumentMapper.ToCredentials(client, _crypto));
                if (file == null) return new DataicoFileResult(502, $"No se pudo descargar el {label} desde Dataico.", null, null, null);
                bytes = file.Value.Bytes;
            }
            else
            {
                bytes = isXml ? DecodeXml(parsed.InlineXml) : DecodeBase64(parsed.InlinePdf);
            }
            if (bytes == null || bytes.Length == 0) return new DataicoFileResult(404, $"Dataico no entregó el {label} de este documento.", null, null, null);

            var safeNumber = new string((input ?? "documento").Trim().Where(char.IsLetterOrDigit).ToArray());
            return new DataicoFileResult(200, null, bytes, isXml ? "application/xml" : "application/pdf", $"{safeNumber}.{(isXml ? "xml" : "pdf")}");
        }

        // Obtiene la factura consultada y la convierte en el borrador que usa el formulario de notas.
        // El UUID interno sale del response de Dataico; los datos de las líneas salen del XML.
        public async Task<(int Status, string? Error, DataicoExternalInvoiceDraft? Draft)> GetInvoiceNoteDraftAsync(Client client, string? input)
        {
            var number = (input ?? string.Empty).Trim();
            var (call, error) = await FetchAsync(client, DataicoLookupKind.Invoice, number, includePdf: false);
            if (error != null) return (error.Value.Status, error.Value.Message, null);
            var parsed = Parse(DataicoLookupKind.Invoice, call!.RawResponse);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Summary.Uuid))
                return (404, $"Dataico no encontró la factura {number} o no devolvió su UUID.", null);

            var xml = await DownloadAsync(client, DataicoLookupKind.Invoice, number, "xml");
            if (!xml.Ok || xml.Bytes == null)
                return (502, xml.Error ?? "Dataico no entregó el XML de la factura.", null);
            try
            {
                var rawXml = System.Text.Encoding.UTF8.GetString(xml.Bytes);
                return (200, null, DataicoExternalInvoiceParser.Parse(rawXml, parsed.Summary.Uuid));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
            {
                return (422, $"No se pudo interpretar el XML de Dataico: {ex.Message}", null);
            }
        }

        private async Task<(Fel.Infrastructure.Dataico.Models.DataicoResult? Call, (int Status, string Message)? Error)> FetchAsync(
            Client client, DataicoLookupKind kind, string? input, bool includePdf)
        {
            var number = (input ?? string.Empty).Trim();
            if (number.Length == 0) return (null, (400, "Escribe el número del documento con su prefijo, por ejemplo DS201."));
            if (client.Integrator?.Code != "DATAICO") return (null, (400, "La consulta en Dataico solo está disponible para clientes que emiten con Dataico."));

            var credentials = DataicoDocumentMapper.ToCredentials(client, _crypto);
            string prefix = string.Empty, digits = string.Empty;
            if (kind != DataicoLookupKind.Invoice && !TrySplit(number, out prefix, out digits))
                return (null, (400, "El número debe llevar el prefijo y terminar en dígitos, por ejemplo DS201."));

            // Nómina: el parámetro staging sigue el ambiente de la cuenta.
            var staging = !string.Equals(client.DataicoEnvironment, "PRODUCCION", StringComparison.OrdinalIgnoreCase);
            var call = kind switch
            {
                DataicoLookupKind.Invoice => await _api.GetInvoiceByNumberAsync(number, credentials),
                DataicoLookupKind.SupportDoc => await _api.GetSupportDocAsync(prefix, digits, credentials),
                DataicoLookupKind.SupportDocAdjustment => await _api.GetSupportDocAdjustmentAsync(prefix, digits, credentials),
                DataicoLookupKind.Payroll => await _api.GetPayrollDocumentAsync("payroll-entries", prefix, digits, staging, includePdf, credentials),
                DataicoLookupKind.PayrollDeletion => await _api.GetPayrollDocumentAsync("payroll-deletions", prefix, digits, staging, includePdf, credentials),
                DataicoLookupKind.PayrollReplacement => await _api.GetPayrollDocumentAsync("payroll-replacements", prefix, digits, staging, includePdf, credentials),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            if (call.Success) return (call, null);
            if (call.StatusCode == 404) return (null, (404, $"Dataico no encontró el documento {number}."));
            return (null, (502, "Dataico no pudo atender la consulta."));
        }

        private sealed class Parsed
        {
            public DataicoLookupSummary Summary { get; init; } = new();
            public string? XmlUrl { get; init; }
            public string? PdfUrl { get; init; }
            public string? InlinePdf { get; init; }
            public string? InlineXml { get; init; }
        }

        private static readonly string[] Wrappers = { "invoice", "invoices", "support_doc", "support_doc_adjustment", "payroll_entry" };

        // La factura y el documento soporte llegan envueltos ("invoice", "support_doc"...); la nómina llega plana. También se acepta una lista.
        private static Parsed? Parse(DataicoLookupKind kind, string raw)
        {
            JsonElement root;
            try { root = JsonDocument.Parse(raw).RootElement.Clone(); }
            catch (JsonException) { return null; }

            if (root.ValueKind == JsonValueKind.Array) root = root.GetArrayLength() > 0 ? root[0] : default;
            if (root.ValueKind == JsonValueKind.Object && !root.TryGetProperty("number", out _))
            {
                foreach (var name in Wrappers)
                {
                    if (!root.TryGetProperty(name, out var inner)) continue;
                    if (inner.ValueKind == JsonValueKind.Array) inner = inner.GetArrayLength() > 0 ? inner[0] : default;
                    if (inner.ValueKind == JsonValueKind.Object) { root = inner; break; }
                }
            }
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("number", out _)) return null;

            var xmlUrl = Text(root, "xml_url");
            var pdfUrl = Text(root, "pdf_url");
            var inlinePdf = Text(root, "pdf");
            var inlineXml = Text(root, "request_xml");
            var messages = new List<string>();
            if (root.TryGetProperty("dian_messages", out var dm) && dm.ValueKind == JsonValueKind.Array)
                messages.AddRange(dm.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).Where(s => s.Length > 0));

            string? customer = null;
            if (root.TryGetProperty("customer", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                var name = Text(c, "company_name") ?? string.Join(" ", new[] { Text(c, "first_name"), Text(c, "family_name") }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var nit = Text(c, "party_identification");
                customer = string.IsNullOrWhiteSpace(name) ? nit : nit == null ? name : $"{name} ({nit})";
            }

            return new Parsed
            {
                XmlUrl = xmlUrl,
                PdfUrl = pdfUrl,
                InlinePdf = inlinePdf,
                InlineXml = inlineXml,
                Summary = new DataicoLookupSummary
                {
                    Number = Text(root, "number") ?? string.Empty,
                    Uuid = Text(root, "uuid"),
                    Cufe = Text(root, "cufe") ?? Text(root, "cude") ?? Text(root, "cune"),
                    Status = Text(root, "dian_status"),
                    IssueDate = Text(root, "issue_date"),
                    Customer = customer,
                    // La respuesta no trae un campo de total: el valor total está en el QR de la DIAN (ValTolFac), en facturas y notas.
                    Total = TotalFromQr(Text(root, "qrcode")),
                    Messages = messages,
                    // El XML sale de su dirección, de la respuesta (nómina) o, en una factura, del documento por su uuid.
                    HasXml = !string.IsNullOrEmpty(xmlUrl) || !string.IsNullOrEmpty(inlineXml) || (kind == DataicoLookupKind.Invoice && !string.IsNullOrEmpty(Text(root, "uuid"))),
                    HasPdf = !string.IsNullOrEmpty(pdfUrl) || !string.IsNullOrEmpty(inlinePdf)
                }
            };
        }

        private static string? Text(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;

        private static string? TotalFromQr(string? qr)
        {
            if (string.IsNullOrEmpty(qr)) return null;
            var m = Regex.Match(qr, @"ValTolFac=([0-9]+(?:\.[0-9]+)?)");
            return m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var total)
                ? total.ToString("N2", CultureInfo.InvariantCulture)
                : null;
        }

        private static byte[]? DecodeBase64(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try { return Convert.FromBase64String(value); } catch (FormatException) { return null; }
        }

        // El XML de la nómina llega como texto o en base64.
        private static byte[]? DecodeXml(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.TrimStart().StartsWith("<") ? System.Text.Encoding.UTF8.GetBytes(value) : DecodeBase64(value);
        }
    }
}
