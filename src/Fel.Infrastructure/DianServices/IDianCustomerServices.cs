using System.Threading.Tasks;

namespace Fel.Infrastructure.DianServices
{
    public interface IDianCustomerServices
    {
        Task<DianResponse> SendTestSetAsync(string fileName, byte[] contentFile, string testSetId);
        Task<DianResponse> SendBillAsync(string fileName, byte[] contentFile);
        Task<DianResponse> GetStatusAsync(string trackId);
    }

    public class DianResponse
    {
        public bool IsValid { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusDescription { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public byte[]? XmlResponse { get; set; }
    }
}
