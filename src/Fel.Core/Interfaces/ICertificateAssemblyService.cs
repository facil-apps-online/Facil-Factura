using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;

namespace Fel.Core.Interfaces;

/// <summary>
/// Descarga el .p7b emitido por el proveedor, lo ensambla con la llave privada generada al crear
/// el CSR (nunca sale de nuestro backend) y lo instala como el <see cref="Certificate"/> vigente
/// del cliente. El password del .p12 resultante se devuelve solo en memoria — nunca se persiste en
/// texto plano — para que el llamador lo use de inmediato (ej. notificarlo por correo) y lo descarte.
/// </summary>
public interface ICertificateAssemblyService
{
    Task<AssembledCertificate> AssembleAndInstallAsync(CertificateRequest request, CancellationToken cancellationToken = default);
}

public sealed record AssembledCertificate(Certificate Certificate, byte[] Pkcs12Bytes, string Password);
