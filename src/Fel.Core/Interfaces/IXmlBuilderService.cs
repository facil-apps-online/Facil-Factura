using Fel.Core.Ubl21.Base;

namespace Fel.Core.Interfaces
{
    public interface IXmlBuilderService
    {
        string BuildXml<T>(T invoice) where T : BaseInvoice;
    }
}
