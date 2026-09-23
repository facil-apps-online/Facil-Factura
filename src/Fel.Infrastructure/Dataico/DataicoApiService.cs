using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Infrastructure.Dataico.Models;

namespace Fel.Infrastructure.Dataico
{
    public interface IDataicoApiService
    {
        Task<DataicoResult> SendInvoiceAsync(DataicoInvoiceRequest invoice, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false);
        Task<DataicoResult> SendCreditNoteAsync(DataicoCreditNoteRequest creditNote, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false);
        Task<DataicoResult> SendDebitNoteAsync(DataicoDebitNoteRequest debitNote, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false);
        Task<DataicoResult> SendSupportDocAsync(DataicoSupportDocumentRequest supportDoc, DataicoCredentials credentials);
        Task<DataicoResult> SendSupportDocAdjustmentAsync(DataicoSupportDocAdjustmentRequest adjustment, DataicoCredentials credentials);
        Task<DataicoResult> SendPayrollAsync(DataicoPayrollRequest payroll, DataicoCredentials credentials);
        Task<DataicoResult> SendPayrollDeletionAsync(DataicoPayrollDeletionRequest deletion, DataicoCredentials credentials);
        Task<DataicoResult> SendPayrollReplacementAsync(DataicoPayrollReplacementRequest replacement, DataicoCredentials credentials);

        // Paso 2 de "documentos personalizados": reenvía el documento ya emitido (identificado por
        // su CUFE/CUNE) con el PDF propio para que Dataico solo despache el correo
        // (send_dian=false, send_email=true). documentTypeCode determina el recurso y la base URL
        // (facturación vs. nómina, que vive en un dominio de API distinto en Dataico) — el mismo
        // patrón {recurso}/{cufe} usado para facturas, aplicado por analogía a documento soporte y
        // nómina: no hay confirmación contra la API real de Dataico de que estos dos acepten este
        // mismo verbo PUT, así que el primer envío de cada uno debe validarse en producción.
        Task<DataicoResult> SendCustomDocumentPdfAsync(string cufe, string documentTypeCode, byte[] pdfBytes, string recipientEmail, DataicoCredentials credentials);
    }

    public class DataicoApiService : IDataicoApiService
    {
        private const string InvoicingBaseUrl = "https://api.dataico.com/direct/dataico_api/v2";
        private const string PayrollBaseUrl = "https://api.dataico.com/direct/payroll-api/v2";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public Task<DataicoResult> SendInvoiceAsync(DataicoInvoiceRequest invoice, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false)
        {
            var envelope = new DataicoInvoiceEnvelope
            {
                actions = new DataicoInvoiceActions { send_dian = sendDian, send_email = sendEmail },
                invoice = invoice
            };
            return ExecuteAsync(HttpMethod.Post, $"{InvoicingBaseUrl}/invoices", credentials, envelope);
        }

        public Task<DataicoResult> SendCreditNoteAsync(DataicoCreditNoteRequest creditNote, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false)
        {
            var envelope = new DataicoCreditNoteEnvelope
            {
                actions = new DataicoInvoiceActions { send_dian = sendDian, send_email = sendEmail },
                credit_note = creditNote
            };
            return ExecuteAsync(HttpMethod.Post, $"{InvoicingBaseUrl}/credit_notes", credentials, envelope);
        }

        public Task<DataicoResult> SendDebitNoteAsync(DataicoDebitNoteRequest debitNote, DataicoCredentials credentials, bool sendDian = true, bool sendEmail = false)
        {
            var envelope = new DataicoDebitNoteEnvelope
            {
                actions = new DataicoInvoiceActions { send_dian = sendDian, send_email = sendEmail },
                debit_note = debitNote
            };
            return ExecuteAsync(HttpMethod.Post, $"{InvoicingBaseUrl}/debit_notes", credentials, envelope);
        }

        public Task<DataicoResult> SendSupportDocAsync(DataicoSupportDocumentRequest supportDoc, DataicoCredentials credentials)
        {
            var envelope = new DataicoSupportDocumentEnvelope { support_doc = supportDoc };
            return ExecuteAsync(HttpMethod.Post, $"{InvoicingBaseUrl}/support_docs", credentials, envelope);
        }

        public Task<DataicoResult> SendSupportDocAdjustmentAsync(DataicoSupportDocAdjustmentRequest adjustment, DataicoCredentials credentials)
        {
            var envelope = new DataicoSupportDocAdjustmentEnvelope { support_doc_adjustment = adjustment };
            return ExecuteAsync(HttpMethod.Post, $"{InvoicingBaseUrl}/support_doc_adjustments", credentials, envelope);
        }

        public Task<DataicoResult> SendPayrollAsync(DataicoPayrollRequest payroll, DataicoCredentials credentials)
        {
            return ExecuteAsync(HttpMethod.Post, $"{PayrollBaseUrl}/payroll-entries", credentials, payroll);
        }

        public Task<DataicoResult> SendPayrollDeletionAsync(DataicoPayrollDeletionRequest deletion, DataicoCredentials credentials)
        {
            return ExecuteAsync(HttpMethod.Post, $"{PayrollBaseUrl}/payroll-deletions", credentials, deletion);
        }

        public Task<DataicoResult> SendPayrollReplacementAsync(DataicoPayrollReplacementRequest replacement, DataicoCredentials credentials)
        {
            return ExecuteAsync(HttpMethod.Post, $"{PayrollBaseUrl}/payroll-replacements", credentials, replacement);
        }

        public Task<DataicoResult> SendCustomDocumentPdfAsync(string cufe, string documentTypeCode, byte[] pdfBytes, string recipientEmail, DataicoCredentials credentials)
        {
            var (baseUrl, resource) = documentTypeCode switch
            {
                "NC" => (InvoicingBaseUrl, "credit_notes"),
                "ND" => (InvoicingBaseUrl, "debit_notes"),
                "DS" => (InvoicingBaseUrl, "support_docs"),
                "DS-AJUSTE" => (InvoicingBaseUrl, "support_doc_adjustments"),
                "NE" => (PayrollBaseUrl, "payroll-entries"),
                "NE-ELIMINACION" => (PayrollBaseUrl, "payroll-deletions"),
                "NE-REEMPLAZO" => (PayrollBaseUrl, "payroll-replacements"),
                _ => (InvoicingBaseUrl, "invoices")
            };

            var envelope = new DataicoCustomDocumentEnvelope
            {
                actions = new DataicoCustomDocumentActions
                {
                    send_dian = false,
                    send_email = true,
                    email = recipientEmail,
                    pdf = Convert.ToBase64String(pdfBytes)
                },
                invoice = new DataicoCustomDocumentInvoiceRef
                {
                    env = credentials.Environment,
                    dataico_account_id = credentials.AccountId
                }
            };

            return ExecuteAsync(HttpMethod.Put, $"{baseUrl}/{resource}/{cufe}", credentials, envelope);
        }

        private async Task<DataicoResult> ExecuteAsync(HttpMethod method, string url, DataicoCredentials credentials, object body)
        {
            try
            {
                var client = DataicoClient.Client;
                using var request = new HttpRequestMessage(method, url);

                request.Headers.Add("Auth-token", credentials.AuthToken);
                if (!string.IsNullOrEmpty(credentials.AccountId))
                {
                    request.Headers.Add("dataico-account-id", credentials.AccountId);
                }

                if (!string.IsNullOrEmpty(credentials.ApiUser))
                {
                    var authBytes = Encoding.UTF8.GetBytes($"{credentials.ApiUser}:{credentials.ApiPassword}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
                }

                var json = JsonSerializer.Serialize(body, JsonOpts);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                var result = new DataicoResult
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    RawResponse = content
                };

                TryExtractIdentifiers(content, result);
                return result;
            }
            catch (Exception ex)
            {
                return new DataicoResult
                {
                    Success = false,
                    StatusCode = 0,
                    RawResponse = string.Empty,
                    ErrorMessage = ex.Message
                };
            }
        }

        // Campos confirmados contra una integración real de Dataico ya en producción (proyecto de
        // migración en C:\FEL, clase ResponseFacturas): cufe, uuid, number, xml_url, pdf_url, qrcode.
        private static void TryExtractIdentifiers(string content, DataicoResult result)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                foreach (var key in new[] { "cufe", "cude", "cune", "trackId" })
                {
                    if (root.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                    {
                        result.Cufe = val.GetString();
                        break;
                    }
                }

                if (root.TryGetProperty("uuid", out var uuidVal) && uuidVal.ValueKind == JsonValueKind.String)
                {
                    result.DataicoDocumentId = uuidVal.GetString();
                }

                if (root.TryGetProperty("xml_url", out var xmlVal) && xmlVal.ValueKind == JsonValueKind.String)
                {
                    result.XmlUrl = xmlVal.GetString();
                }

                if (root.TryGetProperty("pdf_url", out var pdfVal) && pdfVal.ValueKind == JsonValueKind.String)
                {
                    result.PdfUrl = pdfVal.GetString();
                }

                if (root.TryGetProperty("qrcode", out var qrVal) && qrVal.ValueKind == JsonValueKind.String)
                {
                    result.QrCode = qrVal.GetString();
                }

                foreach (var key in new[] { "number", "document_number", "invoice_number" })
                {
                    if (root.TryGetProperty(key, out var val))
                    {
                        result.DocumentNumber = val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
                        break;
                    }
                }

                if (!result.Success && root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                {
                    result.ErrorMessage = msg.GetString();
                }
            }
            catch (JsonException)
            {
                // Respuesta no-JSON (ej. HTML de error de gateway): se conserva solo el RawResponse.
            }
        }
    }
}
