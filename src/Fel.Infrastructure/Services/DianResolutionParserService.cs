using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

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
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public async Task<ParsedResolutionData> ParsePdfAsync(Stream pdfStream)
        {
            var result = new ParsedResolutionData { IsSuccess = false };
            
            try
            {
                string fullText = string.Empty;

                using (var document = PdfDocument.Open(pdfStream))
                {
                    foreach (var page in document.GetPages())
                    {
                        fullText += page.Text + " ";
                    }
                }

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    result.ErrorMessage = "El documento PDF está vacío o no contiene texto legible (posiblemente sea una imagen escaneada).";
                    return result;
                }

                // Normalizar texto para facilitar regex (quitar dobles espacios y saltos de línea raros)
                fullText = Regex.Replace(fullText, @"\s+", " ");

                // 1. Extraer Número de Formulario / Resolución (Típicamente Formulario 1876 o Número de Autorización largo)
                var numMatch = Regex.Match(fullText, @"1876\d{10,14}");
                if (numMatch.Success)
                {
                    result.ResolutionNumber = numMatch.Value;
                }

                // 2. Extraer Prefijo, Rango y Vigencia usando el patrón final del documento
                // Ej: "FACTURA ELECTRÓNICA DE VENTA4 SFRY1,000 2,000 AUTORIZACIÓN 1 24"
                var finalPatternMatch = Regex.Match(fullText, @"([A-Za-z]+)?\s*([\d,\.]+)\s+([\d,\.]+)\s+(AUTORIZACI[OÓ]N|HABILITACI[OÓ]N)\s+\d+\s+(\d+)", RegexOptions.IgnoreCase);
                if (finalPatternMatch.Success)
                {
                    result.Prefix = finalPatternMatch.Groups[1].Value.ToUpper();
                    
                    var startStr = finalPatternMatch.Groups[2].Value.Replace(",", "").Replace(".", "");
                    var endStr = finalPatternMatch.Groups[3].Value.Replace(",", "").Replace(".", "");
                    
                    if (long.TryParse(startStr, out long start)) result.NumberStart = start;
                    if (long.TryParse(endStr, out long end)) result.NumberEnd = end;

                    var monthsStr = finalPatternMatch.Groups[5].Value;
                    int months = 0;
                    int.TryParse(monthsStr, out months);

                    // Buscar fecha de formalización (AAA-MM-DD)
                    var dateMatch = Regex.Match(fullText, @"\d{4}\s*-\d{2}\s*-\d{2}");
                    if (dateMatch.Success)
                    {
                        if (DateTime.TryParse(dateMatch.Value.Replace(" ", ""), out DateTime validFrom))
                        {
                            result.ValidFrom = validFrom;
                            if (months > 0)
                            {
                                result.ValidTo = validFrom.AddMonths(months);
                            }
                        }
                    }
                }
                else 
                {
                    // Fallback a los patrones genéricos (por si el diseño del PDF cambia en otra versión de la DIAN)
                    var prefixMatch = Regex.Match(fullText, @"Prefijo[\s:]*([A-Za-z0-9]+)?", RegexOptions.IgnoreCase);
                    if (prefixMatch.Success && prefixMatch.Groups[1].Success) result.Prefix = prefixMatch.Groups[1].Value;

                    var startMatch = Regex.Match(fullText, @"(?:Desde|Rango Inicial)[\s:]*(\d+)", RegexOptions.IgnoreCase);
                    if (startMatch.Success && long.TryParse(startMatch.Groups[1].Value, out long start)) result.NumberStart = start;

                    var endMatch = Regex.Match(fullText, @"(?:Hasta|Rango Final)[\s:]*(\d+)", RegexOptions.IgnoreCase);
                    if (endMatch.Success && long.TryParse(endMatch.Groups[1].Value, out long end)) result.NumberEnd = end;

                    var dateMatches = Regex.Matches(fullText, @"\d{4}-\d{2}-\d{2}");
                    if (dateMatches.Count >= 2)
                    {
                        if (DateTime.TryParse(dateMatches[0].Value, out DateTime d1) && DateTime.TryParse(dateMatches[1].Value, out DateTime d2))
                        {
                            result.ValidFrom = d1 < d2 ? d1 : d2;
                            result.ValidTo = d1 < d2 ? d2 : d1;
                        }
                    }
                }

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error procesando el PDF: {ex.Message}";
            }

            return result;
        }
    }
}
