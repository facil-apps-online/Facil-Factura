using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Fel.Infrastructure.Services
{
    public class DianResolutionParserService
    {
        public class ParsedResolutionData
        {
            public string ResolutionNumber { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
            public long NumberStart { get; set; }
            public long NumberEnd { get; set; }
            public DateTime? ValidFrom { get; set; }
            public DateTime? ValidTo { get; set; }
            public string? DocumentType { get; set; }
            public string? Modality { get; set; }
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        // Catálogo cerrado de "30. Modalidad" del formulario 1876 — texto fijo de la DIAN, no un
        // catálogo de negocio variable. Solo se mapean las modalidades que este sistema realmente
        // maneja con Resolution.DocumentType; una modalidad no reconocida se deja sin
        // DocumentType (el formulario del frontend obliga a elegirlo antes de guardar).
        private static readonly (string Match, string DocumentType)[] ModalityMap =
        {
            ("FACTURA ELECTR", "FE"),
            ("DOCUMENTO SOPORTE", "POS"),
            ("N[OÓ]MINA", "NE"),
            ("NOTA CR[EÉ]DITO", "NC"),
            ("NOTA D[EÉ]BITO", "ND")
        };

        private static readonly Regex RowPattern = new Regex(
            @"^(?<modalidad>[A-Za-zÁÉÍÓÚÑáéíóúñ\s]+?)\s*(?<modcod>\d+)\s+(?<prefijo>[A-Za-z0-9]*)\s*(?<desde>[\d,\.]+)\s+(?<hasta>[\d,\.]+)\s+(?<vigencia>\d+)\s+(?<tipo>AUTORIZACI[OÓ]N|HABILITACI[OÓ]N|INHABILITACI[OÓ]N)\s+(?<tipocod>\d+)$",
            RegexOptions.IgnoreCase);

        // Devuelve una entrada por cada rango de numeración que trae el PDF — el formulario 1876
        // permite varias filas en la misma hoja (ej. Factura Electrónica + Documento Soporte), y
        // antes esto solo intentaba sacar una con un regex sobre el texto plano concatenado de
        // todo el PDF, que además de no soportar varias filas se rompía apenas había una segunda
        // fila: PdfPig concatena page.Text por ORDEN DE COLUMNA cuando hay varias filas (todas las
        // Modalidades, luego todos los Prefijos, luego...), así que ninguna fila calzaba con el
        // patrón que esperaba una sola fila completa y corrida.
        //
        // La solución real: reconstruir cada fila por posición (coordenadas Top/Left de cada
        // palabra vía page.GetWords()), agrupando por Top (misma fila) y ordenando por Left
        // (orden de lectura real de esa fila) — verificado contra un PDF real de dos filas
        // (WORLD PASS RED S.A.S., FE + Documento Soporte) antes de esta implementación.
        public async Task<List<ParsedResolutionData>> ParsePdfAsync(Stream pdfStream)
        {
            var results = new List<ParsedResolutionData>();
            string fullText;

            try
            {
                fullText = string.Empty;
                var rowTexts = new List<string>();

                using (var document = PdfDocument.Open(pdfStream))
                {
                    foreach (var page in document.GetPages())
                    {
                        fullText += page.Text + " ";

                        var words = page.GetWords().OrderByDescending(w => w.BoundingBox.Top).ToList();
                        var rows = new List<List<Word>>();
                        foreach (var w in words)
                        {
                            var lastRow = rows.Count > 0 ? rows[^1] : null;
                            if (lastRow != null && Math.Abs(lastRow[0].BoundingBox.Top - w.BoundingBox.Top) <= 4)
                            {
                                lastRow.Add(w);
                            }
                            else
                            {
                                rows.Add(new List<Word> { w });
                            }
                        }

                        foreach (var row in rows)
                        {
                            rowTexts.Add(string.Join(" ", row.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    results.Add(new ParsedResolutionData
                    {
                        IsSuccess = false,
                        ErrorMessage = "El documento PDF está vacío o no contiene texto legible (posiblemente sea una imagen escaneada)."
                    });
                    return results;
                }

                fullText = Regex.Replace(fullText, @"\s+", " ");

                // Compartidos por todas las filas del mismo formulario.
                var numMatch = Regex.Match(fullText, @"1876\d{10,14}");
                var resolutionNumber = numMatch.Success ? numMatch.Value : string.Empty;

                DateTime? validFrom = null;
                var dateMatch = Regex.Match(fullText, @"\d{4}\s*-\d{2}\s*-\d{2}");
                if (dateMatch.Success && DateTime.TryParse(dateMatch.Value.Replace(" ", ""), out DateTime parsedDate))
                {
                    validFrom = parsedDate;
                }

                foreach (var rowText in rowTexts)
                {
                    var m = RowPattern.Match(rowText.Trim());
                    if (!m.Success) continue;

                    var modalidad = m.Groups["modalidad"].Value.Trim();
                    var startStr = m.Groups["desde"].Value.Replace(",", "").Replace(".", "");
                    var endStr = m.Groups["hasta"].Value.Replace(",", "").Replace(".", "");
                    int.TryParse(m.Groups["vigencia"].Value, out int months);

                    var documentType = ModalityMap
                        .Where(mm => Regex.IsMatch(modalidad, mm.Match, RegexOptions.IgnoreCase))
                        .Select(mm => mm.DocumentType)
                        .FirstOrDefault();

                    var parsed = new ParsedResolutionData
                    {
                        IsSuccess = true,
                        ResolutionNumber = resolutionNumber,
                        Modality = modalidad,
                        DocumentType = documentType,
                        Prefix = m.Groups["prefijo"].Value.ToUpper(),
                        ValidFrom = validFrom,
                        ValidTo = validFrom.HasValue && months > 0 ? validFrom.Value.AddMonths(months) : null
                    };
                    if (long.TryParse(startStr, out long start)) parsed.NumberStart = start;
                    if (long.TryParse(endStr, out long end)) parsed.NumberEnd = end;

                    results.Add(parsed);
                }

                if (results.Count == 0)
                {
                    results.Add(new ParsedResolutionData
                    {
                        IsSuccess = false,
                        ErrorMessage = "No se pudo reconocer ningún rango de numeración en el PDF. Verifica los datos manualmente."
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new ParsedResolutionData { IsSuccess = false, ErrorMessage = $"Error procesando el PDF: {ex.Message}" });
            }

            return results;
        }
    }
}
