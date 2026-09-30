# Error detectado: país de expedición del representante

## Evidencia

Al solicitar el certificado de Viafirma, la API responde:

```json
{
  "code": "certificate_profile_fields_required",
  "message": "Completa los campos obligatorios del perfil.",
  "fields": [
    "País donde se expidió el documento de identificación del representante"
  ]
}
```

## Causa probable

El formulario de certificaciones ya no debe pedir este campo. Su valor debe salir de `Client.LegalRepresentativeDocumentCountryCode` y enviarse como `countryCode`.

Actualmente el backend usa el valor del cliente con un respaldo que solo aplica cuando es `null`. Si el campo está guardado como cadena vacía (`""`), Viafirma recibe un valor vacío y lo considera faltante.

## Corrección pendiente

En `TenantCertificatesController`, normalizar el país antes de construir los valores del perfil:

```csharp
formValues["countryCode"] = string.IsNullOrWhiteSpace(client.LegalRepresentativeDocumentCountryCode)
    ? "CO"
    : client.LegalRepresentativeDocumentCountryCode.Trim();
```

Además:

- Mantener el campo oculto en el formulario de certificaciones.
- Conservarlo editable únicamente en la información básica del cliente.
- Validar que para Persona Jurídica se haya guardado `CO` u otro código válido antes de enviar a Viafirma.
- Probar nuevamente la solicitud Sandbox con SoFactory.

