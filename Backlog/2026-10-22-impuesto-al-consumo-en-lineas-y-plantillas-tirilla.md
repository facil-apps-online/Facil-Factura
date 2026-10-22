# Impuestos adicionales (INC) en las líneas de factura + plantillas tirilla para factura

Dos pendientes acordados el 2026-10-22. El primero es **urgente**: hoy las facturas salen sin el impuesto al consumo.
La parte 1 no está implementada; la parte 2 (plantillas de tirilla) ya se hizo. Antes de empezar, recordar la regla del proyecto (CLAUDE.md): resumir el enfoque al usuario y esperar su confirmación.

---

## 1. Impuesto al consumo (INC) en las líneas de factura — URGENTE

### Problema

Un producto con impuestos adicionales (el usuario lo dicta como "hipoconsumo", es el impuesto al consumo) no trae ese impuesto al agregarlo a la factura.

### Causa (verificada leyendo el código y la base de producción)

- `Product.Taxes` (`ProductTax`: `TaxCategory` + `Rate`) guarda el impuesto, pero **ninguna línea de documento lo usa**. `DocumentItem` solo tiene IVA (`IvaTreatment`, `TaxRate`, `TaxAmount`) y `Retentions`.
- `DianDocumentMapper.BuildInvoiceData` solo agrupa y genera el impuesto `01` (IVA). `UblGenerator` ya lee un impuesto `04` (INC) para el CUFE (`ValImp2`), pero nadie lo llena.
- `DataicoDocumentMapper` solo envía IVA y retenciones por línea.
- El formulario (`DocumentsPage.tsx`, al elegir producto: `taxRate` / `ivaTreatment`) ignora `p.taxes`.
- En la carga masiva de facturas (`InvoiceImporter`) el `IMP_CONSUMO` del archivo hoy se ignora con un aviso.
- Datos en producción (2026-10-22): 18 productos con `IMP_CONSUMO` (16 al 8 %, uno al 3,5 %, uno al 4 %). Catálogo `OtherTax`: `IMP_CONSUMO` e `IMP_CONSUMO_LICOR`.

Afecta lo que se está emitiendo hoy, con la DIAN directa y con Dataico.

### Plan propuesto

1. **Datos (migración):** tabla `DocumentItemTaxes` (`DocumentItemId`, `TaxCategory`, `Rate`, `BaseAmount`, `Amount`), igual que `DocumentRetention`. Entidad `DocumentItemTax` en `Fel.Core`, `DbSet` y mapeo en `FelDbContext`, y la colección `DocumentItem.AdditionalTaxes`. Crear la migración con
   `FEL_DESIGNTIME_CONNECTION="Server=localhost;Database=FelDb;User Id=sa;Password=x;TrustServerCertificate=True" dotnet ef migrations add <Nombre> -s ../Fel.Api.Client` desde `src/Fel.Infrastructure`.
2. **Cálculo:** base del INC = base de la línea después del descuento (`cantidad × precio × (1 − descuento)`); valor = base × tarifa. No cambia el IVA. `DocumentItem.TotalAmount` = base + IVA + INC. `Document.TotalAmount` = subtotal + IVA + INC (`TaxAmount` sigue siendo solo IVA, o se agrega un campo para el INC; decidir al implementar y mantener coherentes el portal, los mapeadores y el tablero).
3. **Ajuste de centavos:** `RoundingAdjustment.Apply` (`Fel.Core/Models/RoundingAdjustment.cs`) calcula el bruto como base + `TaxAmount` de cada línea. Hay que sumar el INC ahí para que el valor a pagar siga saliendo sin centavos.
4. **Backend de documentos** (`InvoiceController`): `CreateDraft` / `UpdateDraft` ya reemplazan los ítems completos; hacer lo mismo con los impuestos adicionales de cada ítem (asignar `Id` y `DocumentItemId`, igual que las retenciones, y cuidar el patrón de `UpdateDraft` para que EF no genere DELETE explícitos que choquen con la cascada). Incluir `AdditionalTaxes` en los `Include` de lectura, `Publish`, `Resend`, vista previa y reportes.
5. **XML de la DIAN** (`DianDocumentMapper`): por línea, un `TaxSubtotal` con `TaxId = "04"` (INC) con su base, valor y tarifa; en el encabezado, un `TaxTotal` de INC; `TaxInclusiveAmount` = líneas + IVA + INC; `PayableAmount` sale de ahí. Revisar `BaseUblStrategy` (nombre del esquema "INC", ordenamiento de los `TaxTotal`) y el CUFE (`ValImp2` ya espera el `04`). Verificar contra el Anexo Técnico (`Anexo-Tecnico-Factura-Electronica-de-Venta-vr-1-9.pdf`) y los ejemplos de la Caja de Herramientas.
6. **Dataico** (`DataicoDocumentMapper`): agregar el impuesto a `taxes` de cada ítem. **El código de categoría no está confirmado** (se cree que es `IC`; en el Postman de `C:\FEL` solo aparecen `IBUA` e `ICUI`, y el app antiguo solo usa `IVA`, `ICUI` y retenciones). Verificarlo enviando una factura de prueba al sandbox de Dataico con el cliente de pruebas (envía con credenciales demo); el error de validación de Dataico suele listar las categorías válidas. Hay scripts de ejemplo en el scratchpad de la sesión (`send_aiu.sh`), recreables con `probe_get`.
7. **PDF** (`InvoiceReportDataMapper`): una fila "Impuesto al consumo X %" en el desglose de impuestos del pie (misma lista `impuestos` donde van el IVA y las retenciones); sumarlo en el total neto. Revisar `ClientDashboardController` (totales) y `TemplatePreviewSampleData`.
8. **Formulario** (`DocumentsPage.tsx`):
   - Al elegir un producto (también desde el producto rápido), traer sus `taxes` a la línea; permitir quitar el impuesto o cambiar la tarifa en la línea.
   - `recalcItem` / `recalculateTotals`: incluir el INC en el total de la línea y del documento.
   - Resumen de totales y vista del documento: fila "Impuesto al consumo X %".
   - `ProductController` ya incluye `Taxes` al listar productos; confirmar que el listado liviano que usa el formulario también los trae.
9. **Notas crédito y débito:** copian las líneas de la factura (`ReferenceDocumentId`), así que deben copiar también los impuestos adicionales. Revisar `handleCreate*` en `DocumentsPage.tsx` y el cálculo en `InvoiceController` para notas.
10. **Carga masiva de facturas** (`InvoiceImporter`): línea con producto existente → traer los `ProductTax` del producto; producto nuevo con `IMP_CONSUMO` en el archivo → crearlo con ese impuesto y aplicarlo a la línea; quitar el aviso de "se ignoró".
11. **Pruebas:**
    - Harness sin base de datos como el usado para el ajuste de centavos (`round_test`): construir una factura con INC y revisar el XML (`TaxSubtotal` 04, `TaxInclusiveAmount`, `PayableAmount`) y el CUFE.
    - Factura de prueba al sandbox de Dataico con INC.
    - `tsc` de client-web y las 19 pruebas de navegador (`node e2e/run.mjs e2e/adjustment.spec.ts e2e/batch.spec.ts`).

### Fuera de alcance (por ahora)

- `IMP_CONSUMO_LICOR`: el INC de licores depende del grado de alcohol y tiene una parte específica; es otra regla. Hoy no hay productos con él.
- Otros impuestos adicionales (bolsa plástica, ICA, IBUA, ICUI): mismo mecanismo de tabla, pero no se habilitan en este cambio.

### Despliegue (uno a la vez, con autorización)

`fel-migrator` (hay migración) → `fel-api-client` → `client-web` → `fel-api-superadmin` (vista previa de plantillas) → `fel-worker` solo si se toca algo que use. Desde la herramienta PowerShell: `powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1 -Service <svc>`; desde Git Bash falla el `tar`.

---

## 2. Plantillas de tirilla (POS) también para factura electrónica — HECHO (2026-10-22)

Se copiaron las dos plantillas publicadas del tipo `DE-POS` como plantillas globales de factura (`FE-STD`), en **borrador** (`Status = 0`, `MostrarRetenciones = 0`), sin tocar las del POS:

| Plantilla de factura | Llave en Facil Reports | Origen (POS) |
|---|---|---|
| Factura - Tirilla 80mm Clásica | `facilfactura_fe-std_tirilla_clasica_edab1adcae6c4187bc2b5b7da480737d` | `facilfactura_de-pos_e3c6676f386115698e9151951c021047` |
| Factura - Tirilla 80mm QR Arriba | `facilfactura_fe-std_tirilla_qrarriba_950b795a5c294127b2a8e575da12fe9b` | `facilfactura_de-pos_0569e26fc0c0f7b6f28d10846cc9f89e` |

Se bajó el `.repx` publicado de Facil Reports (`GET /api/templates/repx/<llave>`), se subió con la llave nueva (`POST /api/templates/save`) y se insertó la fila en `DocumentTemplates`. Guion: `copiar_tirilla.sh` del scratchpad de la sesión.

**Verificado:** se renderizó con datos de factura (`datos_cop.json`, `POST /api/reports/generate`) y las dos se ven bien. Incluso muestran el bloque de retenciones (RETE FUENTE / RETE ICA) en el pie, así que no hace falta editarlas.

**Pendiente del usuario:** revisarlas en Superadmin > Plantillas y **publicarlas** (están en borrador).

---

## Contexto de la sesión (para quien lo retome)

- Último despliegue (2026-10-22): carga masiva de facturas (`b45d962`) y ajuste de centavos por cliente (`abe594d`), ambos en producción.
- Memoria del proyecto: hablar en español de Colombia; desplegar de a un servicio y solo con autorización explícita; no introducir deuda técnica en la UI; usar el combobox buscable (`SearchableSelect`) y no `<select>` nativo en pantallas nuevas.
