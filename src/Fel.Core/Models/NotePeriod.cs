using System;

namespace Fel.Core.Models
{
    // Período que afecta una nota crédito sin factura de referencia: la DIAN lo exige (notas PERIODO_FAC_INICIAL / PERIODO_FAC_FINAL) y
    // Dataico lo pide como invoice_period_start_date / invoice_period_end_date. Se toma el mes de la fecha de emisión, como hace el
    // legacy (FEL), así el usuario no tiene que capturarlo.
    public static class NotePeriod
    {
        public static (DateTime Start, DateTime End) Of(DateTime issueDate)
        {
            var start = new DateTime(issueDate.Year, issueDate.Month, 1);
            return (start, new DateTime(issueDate.Year, issueDate.Month, DateTime.DaysInMonth(issueDate.Year, issueDate.Month)));
        }
    }
}
