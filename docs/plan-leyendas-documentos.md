# Plan: leyendas configurables en facturas y documentos soporte

## Objetivo

Permitir que el usuario configure un texto de leyenda para que aparezca en la representación gráfica de los documentos electrónicos, por ejemplo:

> Consignar a la cuenta corriente No. XXXXX del banco XXXXX.

La solución tendrá dos niveles:

1. Leyenda general del cliente para facturación electrónica.
2. Leyenda general del cliente para documentos soporte.
3. Leyenda específica por tipo de documento y prefijo, administrada desde la configuración de resoluciones.

La leyenda específica tendrá prioridad sobre la general.

## Regla de selección

Al construir el JSON para imprimir un documento:

```text
leyenda = leyenda específica del cliente + tipo de documento + prefijo
          ?? leyenda general del cliente para ese tipo de documento
          ?? vacío
```

La búsqueda específica debe usar `ClientId`, `DocumentType` y `Prefix`. No debe depender de `ResolutionId`.

Esto permite que una nueva resolución con el mismo tipo de documento y prefijo conserve automáticamente la leyenda configurada anteriormente.

Ejemplos:

| Cliente | Tipo | Prefijo | Resolución | Resultado |
|---|---|---|---|---|
| A | FE | FEG | Resolución 1 | Leyenda configurada para FE/FEG |
| A | FE | FEG | Resolución 2 | La misma leyenda FE/FEG |
| A | DS | DS | Resolución 3 | Leyenda específica DS/DS o general DS |
| A | FE | ABC | Resolución 4 | Leyenda general FE si no existe ABC |

## Diseño de datos

### Campos generales en `Client`

Agregar dos campos opcionales:

- `ElectronicInvoiceLegend`: leyenda general para factura electrónica y, si aplica, notas asociadas a facturación electrónica.
- `SupportDocumentLegend`: leyenda general para documento soporte.

Los campos deben aceptar texto largo, conservar saltos de línea y permitir valor vacío/null.

### Nueva tabla de configuración por prefijo

Crear una entidad independiente, por ejemplo `DocumentLegendByPrefix`:

- `Id` (`Guid`)
- `ClientId` (`Guid`)
- `DocumentType` (`string`): `FE`, `DS` y los códigos de ajuste que deban compartir la leyenda
- `Prefix` (`string`)
- `Text` (`string`)
- `CreatedAt`
- `UpdatedAt`

Crear un índice único por:

```text
(ClientId, DocumentType, Prefix)
```

El prefijo debe normalizarse con `Trim()` y mayúsculas antes de guardar y consultar.

No se recomienda añadir el texto directamente a `Resolution`, porque una nueva resolución tendría otro `ResolutionId` y perdería la configuración anterior.

## Resolución del tipo de documento

Definir una función de normalización para que los códigos de resolución y documento se comparen consistentemente:

- Factura electrónica: `FE` / `FE-STD` → `FE`.
- Documento soporte: `DS` / código estándar equivalente → `DS`.
- Ajustes: decidir si `NC`, `ND`, `DS-NC` y `DS-ND` heredan la leyenda de su documento base o tienen configuración independiente.

La decisión inicial recomendada es:

- `FE`, `NC` y `ND` usan la configuración de facturación electrónica.
- `DS` y sus notas de ajuste usan la configuración de documento soporte.

Dejar esta regla centralizada en un servicio para no duplicarla en los controladores.

## Flujo de impresión

1. Obtener el cliente y la resolución seleccionada por el documento.
2. Determinar el tipo normalizado (`FE` o `DS`) y el prefijo efectivo.
3. Consultar la leyenda específica por `ClientId + tipo + prefijo`.
4. Si está vacía o no existe, consultar el campo general correspondiente del cliente.
5. Enviar el resultado en el JSON del reporte, obligatoriamente en `Documento.Leyenda`:

```json
{
  "Documento": {
    "Leyenda": "Consignar a la cuenta corriente ..."
  }
}
```

6. La plantilla REPX debe enlazar el campo como `[Documento.Leyenda]`.

`Documento.Leyenda` será el contrato único para la representación gráfica. No se deben crear campos separados como `LeyendaGeneral`, `LeyendaResolucion` o `LeyendaPrefijo` en el JSON final; esos valores solo participan internamente en la resolución de prioridad.

El campo debe estar disponible en todos los caminos que generan representación gráfica:

- Factura electrónica.
- Documento soporte.
- Notas de crédito/débito si heredan la configuración de FE.
- Documento soporte de ajuste si hereda la configuración de DS.
- Generación mediante Dataico.
- Generación mediante integración nativa.

La leyenda de impresión no debe modificar el contenido del QR ni el XML DIAN en esta primera fase, salvo que posteriormente se solicite incluirla también en la información fiscal transmitida.

## API y pantallas

### Configuración del cliente

En la pantalla de configuración/edición del cliente agregar:

- Leyenda general de factura electrónica.
- Leyenda general de documento soporte.

Usar `textarea`, mostrar contador o límite de caracteres y permitir saltos de línea.

### Configuración desde resoluciones

En las pantallas de resoluciones del cliente y del tenant:

- Mostrar la leyenda asociada al par `tipo + prefijo`.
- Permitir crear o editar la leyenda.
- Si ya existe otra resolución con el mismo tipo y prefijo, mostrar la misma configuración.
- Al crear una nueva resolución con el mismo tipo y prefijo, reutilizar automáticamente la leyenda existente.

Endpoints sugeridos:

```text
GET  /client/document-legends
PUT  /client/document-legends/{documentType}/{prefix}
GET  /tenant/clients/{clientId}/document-legends
PUT  /tenant/clients/{clientId}/document-legends/{documentType}/{prefix}
```

La autorización debe respetar el alcance actual: el cliente administra sus propios datos y el tenant administra los de sus clientes.

## Migración y compatibilidad

1. Crear migración para los dos campos generales de `Client`.
2. Crear migración para `DocumentLegendByPrefix`, índice único y relaciones.
3. Inicializar los campos nuevos como null/vacío.
4. Mantener compatibilidad con plantillas existentes: si no tienen `[Documento.Leyenda]`, no cambia su comportamiento.
5. La leyenda debe enviarse como `null` o cadena vacía cuando no exista para que la plantilla pueda ocultar el control.

## Plantilla REPX

En el formato de factura y documento soporte:

- Agregar un `XRLabel` enlazado a `[Documento.Leyenda]`.
- Activar `Multiline` y `CanGrow`.
- Ocultar el control cuando `Len([Documento.Leyenda]) = 0`.
- Ubicarlo en una banda estable, sin desplazar indebidamente totales, QR o subreportes.
- Validar saltos de línea, textos largos y ausencia de leyenda.

La incorporación del campo al JSON debe hacerse antes de publicar o editar el REPX, para que el diseñador pueda verlo en el Field List.

## Pruebas

### Persistencia

- Crear y editar leyenda general FE.
- Crear y editar leyenda general DS.
- Crear leyenda específica FE/FEG.
- Crear una nueva resolución FE/FEG y verificar que conserve la leyenda.
- Crear FE con otro prefijo y verificar fallback a la leyenda general.
- Verificar que cliente A no vea ni utilice leyendas del cliente B.

### Generación

- Factura con leyenda específica.
- Factura sin específica y con general.
- Factura sin ninguna leyenda.
- Documento soporte con específica.
- Documento soporte con fallback general.
- Nota de crédito/débito y ajuste de documento soporte.
- Impresión nativa y Dataico.
- Vista previa con leyenda de ejemplo.
- Texto largo, caracteres especiales y saltos de línea.

### Regresión

- QR.
- Subreporte de impuestos.
- Totales y retenciones.
- Plantillas antiguas sin el campo nuevo.

## Orden de implementación

1. Definir la normalización de tipos (`FE`/`DS`) y la regla de herencia para notas.
2. Crear entidades, campos, índice y migración.
3. Crear servicio de resolución de leyendas con prioridad específica → general.
4. Agregar endpoints para cliente y tenant.
5. Agregar controles en configuración del cliente y resoluciones.
6. Incorporar `Documento.Leyenda` en todos los mapeadores de reportes.
7. Actualizar datos de vista previa.
8. Ajustar los REPX de factura y documento soporte.
9. Ejecutar pruebas y revisar documentos reales.
10. Desplegar por servicio: API client/tenant, portales correspondientes y FacilReports si cambian los formatos.

## Decisiones pendientes antes de ejecutar

- Confirmar si NC/ND deben usar siempre la leyenda FE o tener leyendas independientes.
- Confirmar si los textos generales deben aplicar también a nómina electrónica.
- Definir límite máximo de caracteres y si se aceptará HTML o únicamente texto plano.
- Definir si la leyenda debe aparecer también en XML/notificaciones o únicamente en el PDF/representación gráfica.
