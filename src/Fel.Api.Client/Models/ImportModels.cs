using System.Collections.Generic;

namespace Fel.Api.Client.Models
{
    public class ImportRowResult
    {
        public int Row { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ImportSummary
    {
        public int TotalRows { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<ImportRowResult> Results { get; set; } = new();
    }
}
