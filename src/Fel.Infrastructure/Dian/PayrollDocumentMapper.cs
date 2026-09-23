using Fel.Core.Entities;
using Fel.Core.Models;

namespace Fel.Infrastructure.Dian
{
    // Convierte PayrollRequest/PayrollVoidRequest (API B2B) a UblPayrollData/UblPayrollVoidData. El
    // Empleador siempre sale del Client autenticado, igual que DianDocumentMapper para facturas —
    // nunca de lo que declare el caller.
    public static class PayrollDocumentMapper
    {
        public static UblPayrollData BuildPayrollDataFromRequest(PayrollRequest request, Client client)
        {
            return new UblPayrollData
            {
                DocumentNumber = request.DocumentNumber,
                Prefix = request.Prefix,
                IssueDate = request.IssueDate,
                IssueTime = request.IssueDate,
                SoftwareId = client.SoftwareId,
                SoftwarePin = client.SoftwarePin,
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                DianCode = "102",
                Employer = BuildEmployer(client),
                Worker = request.Worker,
                Period = request.Period,
                Payment = request.Payment,
                PaymentDates = request.PaymentDates,
                Earnings = request.Earnings,
                Deductions = request.Deductions,
                Rounding = request.Rounding,
                EarningsTotal = request.EarningsTotal,
                DeductionsTotal = request.DeductionsTotal,
                PayableTotal = request.PayableTotal,
                Notes = request.Notes
            };
        }

        public static UblPayrollVoidData BuildPayrollVoidDataFromRequest(PayrollVoidRequest request, Client client)
        {
            return new UblPayrollVoidData
            {
                DocumentNumber = request.DocumentNumber,
                Prefix = request.Prefix,
                IssueDate = request.IssueDate,
                IssueTime = request.IssueDate,
                SoftwareId = client.SoftwareId,
                SoftwarePin = client.SoftwarePin,
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                Employer = BuildEmployer(client),
                PredecessorNumber = request.PredecessorNumber,
                PredecessorCune = request.PredecessorCune,
                PredecessorIssueDate = request.PredecessorIssueDate,
                Notes = request.Notes
            };
        }

        private static PayrollPartyData BuildEmployer(Client client) => new PayrollPartyData
        {
            TaxId = client.TaxId,
            VerificationDigit = client.VerificationDigit,
            CompanyName = client.CompanyName,
            CountryCode = "CO",
            DepartmentCode = DianDocumentMapper.DeriveDepartmentCode(client.CityCode),
            CityCode = client.CityCode ?? string.Empty,
            Address = client.Address
        };
    }
}
