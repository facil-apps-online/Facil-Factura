using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace Fel.Core.Interfaces
{
    public interface IXmlSignerService
    {
        string SignXmlXadesEpes(string xmlString, X509Certificate2 certificate);
    }
}
