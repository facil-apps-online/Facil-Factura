using System;
using System.Text;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.DianServices;
using System.IO;
using System.IO.Compression;

namespace Fel.Infrastructure.Services
{
    public class DianIntegrationService
    {
        private readonly IDianCustomerServices _dianClient;

        public DianIntegrationService(IDianCustomerServices dianClient)
        {
            _dianClient = dianClient;
        }

        public async Task<DianResponse> SendInvoiceToDianAsync(string xmlSigned, string invoiceNumber, string testSetId = "")
        {
            // DIAN expects the XML to be compressed in a ZIP file
            var zipBytes = CreateZipArchive(xmlSigned, invoiceNumber);
            var zipFileName = $"{invoiceNumber}.zip";

            if (!string.IsNullOrEmpty(testSetId))
            {
                // Entorno de habilitación (Set de Pruebas)
                return await _dianClient.SendTestSetAsync(zipFileName, zipBytes, testSetId);
            }
            else
            {
                // Entorno de Producción
                return await _dianClient.SendBillAsync(zipFileName, zipBytes);
            }
        }

        private byte[] CreateZipArchive(string xmlContent, string documentNumber)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var zipEntry = archive.CreateEntry($"{documentNumber}.xml", CompressionLevel.Fastest);
                using var entryStream = zipEntry.Open();
                var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
                entryStream.Write(xmlBytes, 0, xmlBytes.Length);
            }
            return memoryStream.ToArray();
        }
    }
}
