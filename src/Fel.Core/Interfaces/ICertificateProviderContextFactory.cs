using System.Threading;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces;

public interface ICertificateProviderContextFactory
{
    Task<CertificateProviderContext> CreateAsync(string providerKey, string environment, CancellationToken cancellationToken = default);
}
