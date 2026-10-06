using Fel.Api.Tenant.DTOs;
using Fel.Core.Entities;

namespace Fel.Api.Tenant.Services.MinSalud
{
    public interface IMinSaludMuvService
    {
        Task<(bool IsSuccess, string TrackingId, string Message, string JsonPayload)> SendRipsAsync(RipsEmitRequest request, Client client, Fel.Infrastructure.Services.MinSaludCredentials credentials);
    }
}
