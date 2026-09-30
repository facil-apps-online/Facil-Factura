namespace Fel.Core.Interfaces
{
    public interface IUblGenerator
    {
        string GenerateInvoiceXml(Models.UblInvoiceData data);

        // Misma fórmula que ya queda embebida dentro del XML de GenerateInvoiceXml, expuesta
        // aparte para que el llamador pueda guardar el CUFE en el Document sin volver a parsear
        // el XML generado.
        string CalculateCufe(Models.UblInvoiceData data);

        // Contenido completo del código QR de la representación gráfica impresa (Anexo Técnico
        // v1.9, numeral 11: NumFac/FecFac/HorFac/NitFac/DocAdq/ValFac/ValIva/ValOtroIm/ValTolFac/
        // CUFE/QRCode=<url>) — NO es lo mismo que el <sts:QRCode> del XML (solo la URL, numeral
        // 11.7.1, ver BaseUblStrategy). Cuando se emite vía Dataico, ellos ya lo devuelven armado;
        // esto es para cuando se emite directo a la DIAN con certificado propio, donde nadie más lo
        // arma.
        string BuildGraphicQrContent(Models.UblInvoiceData data, string cufe);

        // Documento Equivalente Electrónico (Resolución 000165 de 2023, Anexo Técnico v1.0 — ver
        // docs/dian-doc-equivalente/) reusa el mismo XML de Invoice pero con una fórmula de CUFE/CUDE
        // propia (sin el término de total de impuestos, y con el Software-PIN en vez de la Clave
        // Técnica de la resolución).
        string CalculateEquivalentDocumentCufe(Models.UblInvoiceData data);

        // Documento Soporte en adquisiciones a no obligados a facturar (Resolución 000167 de 2021,
        // Anexo Técnico v1.1 — ver docs/dian-doc-soporte/). CUDS con fórmula propia: solo considera
        // IVA (no INC/ICA), y el orden NIT vendedor→NIT adquirente en vez de emisor→adquirente.
        string CalculateCuds(Models.UblInvoiceData data);

        // Eventos de Recepción RADIAN (ApplicationResponse) — Anexo Técnico de Factura Electrónica
        // v1.9, numeral 6.5 y 11.5 (ver docs/dian-radian/). Esquema y CUDE propios.
        string GenerateEventXml(Models.UblEventData data, string cude);
        string CalculateEventCude(Models.UblEventData data);

        // Nómina Electrónica no es UBL Invoice-family (esquema propio de la DIAN), así que tiene su
        // propio par de métodos en vez de pasar por GenerateInvoiceXml/CalculateCufe.
        string GeneratePayrollXml(Models.UblPayrollData data, string cune);
        string CalculateCune(Models.UblPayrollData data);

        string GeneratePayrollVoidXml(Models.UblPayrollVoidData data, string cune);
        string CalculateVoidCune(Models.UblPayrollVoidData data);
    }
}
