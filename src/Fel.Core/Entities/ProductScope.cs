namespace Fel.Core.Entities;

// Familias funcionales del catálogo de productos del portal de clientes.
// Invoice agrupa factura electrónica y sus notas; Support agrupa documento
// soporte y notas de ajuste; Payroll queda reservado para nómina electrónica.
public enum ProductScope
{
    Invoice = 0,
    Support = 1,
    Payroll = 2
}
