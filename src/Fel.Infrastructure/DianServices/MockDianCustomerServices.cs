using System.Threading.Tasks;

namespace Fel.Infrastructure.DianServices
{
    // Mock implementation for development and until WCF connects successfully
    public class MockDianCustomerServices : IDianCustomerServices
    {
        public Task<DianResponse> SendTestSetAsync(string fileName, byte[] contentFile, string testSetId)
        {
            return Task.FromResult(new DianResponse
            {
                IsValid = true,
                StatusCode = "00",
                StatusDescription = "Procesado Correctamente",
                ErrorMessage = ""
            });
        }

        public Task<DianResponse> SendBillAsync(string fileName, byte[] contentFile)
        {
            return Task.FromResult(new DianResponse
            {
                IsValid = true,
                StatusCode = "00",
                StatusDescription = "Procesado Correctamente",
                ErrorMessage = ""
            });
        }

        public Task<DianResponse> GetStatusAsync(string trackId)
        {
            return Task.FromResult(new DianResponse
            {
                IsValid = true,
                StatusCode = "00",
                StatusDescription = "Procesado",
                ErrorMessage = ""
            });
        }
    }
}
