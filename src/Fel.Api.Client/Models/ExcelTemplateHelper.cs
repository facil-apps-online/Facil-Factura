using ClosedXML.Excel;

namespace Fel.Api.Client.Models
{
    // Boilerplate compartido para generar las plantillas Excel descargables (terceros,
    // productos, facturas, nómina) con un estilo consistente.
    public static class ExcelTemplateHelper
    {
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static void WriteHeaderRow(IXLWorksheet sheet, int row, params string[] headers)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            }
            sheet.SheetView.FreezeRows(row);
        }

        public static void AddDropdown(IXLWorksheet sheet, string range, params string[] options)
        {
            var validation = sheet.Range(range).CreateDataValidation();
            validation.List(string.Join(",", options));
        }

        public static byte[] ToBytes(XLWorkbook workbook)
        {
            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
