namespace Fel.Core.Interfaces
{
    public interface IUblGenerator
    {
        string GenerateInvoiceXml(Models.UblInvoiceData data);
    }
}
