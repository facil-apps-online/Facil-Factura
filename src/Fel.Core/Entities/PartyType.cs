namespace Fel.Core.Entities
{
    public enum PartyType
    {
        Cliente = 0,
        Proveedor = 1,
        Empleado = 2,
        // Tercero por cuenta de quien se factura en una factura de mandato (operación 11 de la DIAN).
        Mandante = 3
    }
}
