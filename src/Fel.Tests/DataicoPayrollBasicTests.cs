using System.Text.Json;
using Fel.Core.Entities;
using Fel.Infrastructure.Dataico;

namespace Fel.Tests
{
    // Con un mes completo de licencia no remunerada el salario básico queda en cero: Dataico exige days y amount en base-salary, así que el cero va explícito.
    public class DataicoPayrollBasicTests
    {
        private static JsonElement Accruals(params DataicoDocumentMapper.PayrollConceptInput[] accruals)
        {
            var employee = new Customer { IdentificationNumber = "1", IdentificationType = "13", Name = "MARTHA GALLO", CityCode = "11001" };
            var client = new Client { Id = Guid.NewGuid() };
            var request = DataicoDocumentMapper.BuildPayrollRequest(employee, client, "N", 51, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31),
                new DateTime(2026, 10, 29), new DateTime(2026, 10, 30), accruals, Array.Empty<DataicoDocumentMapper.PayrollConceptInput>(),
                Array.Empty<IdentificationType>(), Array.Empty<PayrollConceptType>());
            return JsonSerializer.SerializeToElement(request).GetProperty("accruals");
        }

        [Fact]
        public void A_zero_basic_salary_sends_days_and_amount_explicitly()
        {
            var accruals = Accruals(
                new DataicoDocumentMapper.PayrollConceptInput { Code = "BASICO", Amount = 0 },
                new DataicoDocumentMapper.PayrollConceptInput { Code = "LICENCIA_NO_REMUNERADA", Days = 30, InitialDate = "01/10/2026", FinalDate = "31/10/2026" });

            var basic = accruals[0];
            Assert.Equal(0, basic.GetProperty("days").GetInt32());
            Assert.Equal(0m, basic.GetProperty("amount").GetDecimal());
        }

        [Fact]
        public void Other_concepts_still_omit_a_zero_amount()
        {
            var accruals = Accruals(new DataicoDocumentMapper.PayrollConceptInput { Code = "BONIFICACION", Amount = 0 });

            Assert.False(accruals[0].TryGetProperty("amount", out _));
        }
    }
}
