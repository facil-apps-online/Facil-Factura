namespace Fel.Core.Entities
{
    // Tratamiento de IVA de un ítem, según el catálogo estándar DIAN/Dataico.
    public enum IvaTreatment
    {
        Gravado = 0,  // Tiene tarifa de IVA aplicable (ej. 19%, 5%)
        Exento = 1,   // Sujeto a IVA pero a tarifa 0% (se envía el impuesto con tarifa 0)
        Excluido = 2  // Fuera del ámbito del IVA (no se envía impuesto de IVA)
    }
}
