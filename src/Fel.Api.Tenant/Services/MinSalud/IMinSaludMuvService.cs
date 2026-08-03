using Fel.Api.Tenant.DTOs;

namespace Fel.Api.Tenant.Services.MinSalud
{
    public interface IMinSaludMuvService
    {
        Task<(bool IsSuccess, string TrackingId, string Message, string JsonPayload)> SendRipsAsync(RipsEmitRequest request);
    }
}
