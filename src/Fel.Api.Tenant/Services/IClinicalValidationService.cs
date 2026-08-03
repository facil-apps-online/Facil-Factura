using Fel.Api.Tenant.DTOs;

namespace Fel.Api.Tenant.Services
{
    public interface IClinicalValidationService
    {
        Task<List<string>> ValidateRipsAsync(RipsEmitRequest request);
    }
}
