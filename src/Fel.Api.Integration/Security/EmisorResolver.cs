using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Infrastructure.Services;

namespace Fel.Api.Integration.Security
{
    /// <summary>
    /// Resultado de resolver el emisor autenticado, su resolución y su certificado.
    /// Si <see cref="Error"/> viene con algo, el controlador debe devolverlo tal cual.
    /// </summary>
    public sealed class ResultadoEmisor
    {
        public Client? Client { get; init; }
        public Resolution? Resolution { get; init; }
        // Dónde emite: la ubicación de la sucursal dueña de la llave de API (o la del Client si la sucursal no tiene propia).
        public EmitterLocation? Location { get; init; }
        public X509Certificate2? Certificate { get; init; }
        public X509Certificate2Collection? CertificateChain { get; init; }
        public IActionResult? Error { get; init; }

        /// <summary>
        /// El emisor es un Client de prueba de developer: no hay certificado y no lo habrá nunca,
        /// así que el controlador debe simular la respuesta en vez de firmar y transmitir.
        /// </summary>
        public bool EsSandbox { get; init; }

        public static ResultadoEmisor Ok(Client client, EmitterLocation location, Resolution? resolution, X509Certificate2 certificate, X509Certificate2Collection certificateChain) =>
            new() { Client = client, Location = location, Resolution = resolution, Certificate = certificate, CertificateChain = certificateChain };

        public static ResultadoEmisor Sandbox(Client client, EmitterLocation location, Resolution? resolution) =>
            new() { Client = client, Location = location, Resolution = resolution, EsSandbox = true };

        public static ResultadoEmisor Fail(IActionResult error) => new() { Error = error };
    }

    /// <summary>
    /// Resuelve emisor + resolución + certificado para los endpoints de la API de integración.
    /// </summary>
    /// <remarks>
    /// Antes esta misma secuencia estaba copiada en PayrollController, SupportDocumentsController y
    /// (sin siquiera extraerse a un método) EquivalentDocumentsController, con tres redacciones
    /// distintas de los mismos mensajes de error. Unificarla evita que un cambio de criterio — como
    /// el soporte de sandbox que se agregó acá — haya que repetirlo en tres sitios y se olvide en uno.
    /// </remarks>
    public static class EmisorResolver
    {
        /// <param name="documentTypeCode">
        /// Código del tipo de documento cuya resolución hay que buscar. Si es null no se busca
        /// resolución (la nómina electrónica no lleva).
        /// </param>
        /// <param name="nombreTipoDocumento">Cómo nombrar el documento en el mensaje de error.</param>
        public static async Task<ResultadoEmisor> ResolverAsync(
            ControllerBase controlador,
            FelDbContext dbContext,
            ICryptoVault cryptoVault,
            string? documentTypeCode = null,
            string? prefix = null,
            string nombreTipoDocumento = "documento")
        {
            if (controlador.HttpContext.Items["ClientId"] is not string clientIdStr || !Guid.TryParse(clientIdStr, out var clientId))
            {
                return ResultadoEmisor.Fail(controlador.Unauthorized(new { message = "No se pudo resolver el emisor autenticado." }));
            }

            var client = await dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
            {
                return ResultadoEmisor.Fail(controlador.Unauthorized(new { message = "Emisor no encontrado." }));
            }

            if (client.Integrator.Code != "NATIVE")
            {
                return ResultadoEmisor.Fail(controlador.UnprocessableEntity(new
                {
                    message = $"Este emisor tiene configurado el proveedor '{client.Integrator.Code}', no emisión directa a la DIAN (NATIVE)."
                }));
            }

            var location = await BranchProvisioning.LocationAsync(dbContext, client, controlador.HttpContext.GetBranchId());

            Resolution? resolution = null;
            if (documentTypeCode != null)
            {
                resolution = await dbContext.Resolutions.ForBranch(dbContext, controlador.HttpContext.GetBranchId()).FirstOrDefaultAsync(
                    r => r.ClientId == clientId && r.IsActive && r.DocumentType == documentTypeCode && r.Prefix == prefix);

                // A un Client de prueba tampoco se le exige tener resolución cargada: si no la
                // tiene se sigue de largo y el documento se simula igual.
                if (resolution == null && !client.IsDeveloperSandbox)
                {
                    return ResultadoEmisor.Fail(controlador.UnprocessableEntity(new
                    {
                        message = $"No hay una resolución de {nombreTipoDocumento} ({documentTypeCode}) activa para el prefijo '{prefix}'."
                    }));
                }
            }

            var certificate = await dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
            {
                // Un Client de prueba de developer nunca va a tener certificado: devolver 422 deja
                // la API inservible para quien está construyendo su integración. Se le simula la
                // respuesta, igual que hacen los RIPS (ver Fel.Infrastructure SandboxSimulation).
                if (client.IsDeveloperSandbox)
                {
                    return ResultadoEmisor.Sandbox(client, location, resolution);
                }

                return ResultadoEmisor.Fail(controlador.UnprocessableEntity(new
                {
                    message = "Este emisor no tiene un certificado digital activo cargado."
                }));
            }

            var cert = cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            var certificateNit = cryptoVault.ExtractNit(cert);
            if (!NitValidation.Matches(certificateNit, client.TaxId))
            {
                return ResultadoEmisor.Fail(controlador.UnprocessableEntity(new
                {
                    message = $"El NIT del certificado ({certificateNit ?? "no encontrado"}) no coincide con el NIT registrado del emisor ({client.TaxId})."
                }));
            }

            var certChain = cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
            return ResultadoEmisor.Ok(client, location, resolution, cert, certChain);
        }
    }
}
