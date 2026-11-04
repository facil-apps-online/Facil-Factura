# Manual de Integración B2B - Facil-Factura.pro API

Bienvenido al manual de integración técnica para la emisión de Documentos Electrónicos (DIAN y MinSalud) a través de Facil-Factura.pro. Esta API RESTful está diseñada para procesar altos volúmenes de documentos de forma asíncrona, garantizando tiempos de respuesta instantáneos.

## 1. Autenticación

La API usa dos credenciales por sucursal, que se envían en headers sobre HTTPS, y un control de tasa (Rate Limiting de 100 req/seg). No hay firma ni marca de tiempo.

Todas las peticiones deben incluir:

*   `x-api-key`: tu API Key (la de pruebas empieza por `test_`).
*   `x-api-secret`: el API Secret de esa misma llave.

```bash
curl https://api.facil-factura.pro/api/co/dian/documents/{uuid} \
  -H "x-api-key: TU_API_KEY" \
  -H "x-api-secret: TU_API_SECRET"
```

Guarda el secreto solo en tu servidor. Errores: `401` con `code` `MISSING_CREDENTIALS`, `INVALID_API_KEY` o `INVALID_API_SECRET`, y `403` `ACCOUNT_BLOCKED`.

---

## 2. Flujo de emisión (síncrono)

La emisión de la factura es síncrona: la respuesta del `POST` ya trae el resultado final ante la DIAN (o ante el integrador que tenga configurado el emisor).

1.  **Emisión:** `POST /api/co/dian/invoices` con el JSON de la factura. Respuesta `201` con el `id` (UUID), el número, el estado y el CUFE. El documento también aparece en el portal del emisor.
2.  **Consulta:** `GET /api/co/dian/documents/{id}` devuelve el estado, la respuesta de la DIAN y el CUFE. Solo ves los documentos de tu propio emisor; cualquier otro `id` responde `404`.
3.  **Descarga:** `GET /api/co/dian/documents/{id}/files` entrega en Base64 el PDF (`pdfBase64`), el XML firmado (`xmlBase64`), la respuesta de la DIAN (`dianResponseBase64`) y el AttachedDocument (`attachedDocumentBase64`). Si el PDF no se pudo generar, `pdfBase64` va en `null` y `pdfError` dice por qué (por ejemplo, falta elegir la plantilla). Con `?include=files` en la emisión, la respuesta del `POST` ya trae esos archivos en `files`. También hay descargas directas: `GET /api/co/dian/documents/{id}/pdf` (`application/pdf`; `422 PDF_NOT_AVAILABLE` si no se pudo generar) y `GET /api/co/dian/documents/{id}/xml` (`application/xml`).
4.  **Envío por correo:** `POST /api/co/dian/documents/{id}/send` manda el PDF al adquirente. Para una factura con PDF propio, emítela con `send_email: false` (no sale correo), arma tu PDF con el CUFE y el QR, y envíalo aquí en `pdf_base64`; sin él se usa el PDF de la plantilla del emisor. Acepta `to` (por defecto el correo del adquirente), `cc` (hasta 3, solo emisión directa), `subject` (solo emisión directa) y `attachments` (PDF extra, cada uno con `name` y `content_base64`). Solo documentos aprobados; con emisión directa hay un tope de 5 envíos por documento cada 24 horas (`429`).

**Consecutivo.** El `number` lo envías tú y lo controlas desde tu sistema. Puede llegar en cualquier orden (el 5 antes que el 4) mientras esté dentro del rango de la resolución habilitada y vigente. Consulta tus resoluciones con `GET /api/co/dian/resolutions`.

**Número repetido.** Si el número ya fue usado por un documento aprobado o en proceso, la respuesta es `409 DOCUMENT_ALREADY_EXISTS` con el `id`, el estado, el CUFE y la respuesta que tengamos de ese documento; no se emite uno nuevo. Si el documento anterior quedó en borrador o rechazado, reenviar el mismo número corrige y reintenta sobre el mismo `id`.

**Errores en la emisión.** `400 VALIDATION_ERROR` (el arreglo `details` nombra cada campo), `422 RESOLUTION_NOT_FOUND | NUMBER_OUT_OF_RANGE | RESOLUTION_EXPIRED | PROVIDER_REJECTED | TEMPLATE_REQUIRED | CANNOT_ISSUE`, `409 DOCUMENT_ALREADY_EXISTS | REQUEST_IN_PROGRESS`, `502 PROVIDER_UNAVAILABLE` (no se pudo confirmar: no reenvíes, consulta el estado con el `id`).

---

## 3. Catálogo de Endpoints

URL Base: `https://api.facil-factura.pro` (Sujeto a ambiente QA/PROD)

Las rutas llevan el prefijo `co/dian` (país/autoridad regulatoria) para poder sumar otros países sin
romper las existentes — ej. mañana `api/mx/sat/invoices`.

| Documento | Endpoint | Método | Estado |
| :--- | :--- | :--- | :--- |
| **Factura de Venta** (estándar, mandato, exportación) | `/api/co/dian/invoices` | POST | Disponible (contrato nuevo) |
| **Consulta de un documento por UUID** | `/api/co/dian/documents/{id}` | GET | Disponible |
| **Archivos de un documento (Base64)** | `/api/co/dian/documents/{id}/files` | GET | Disponible |
| **XML de un documento** | `/api/co/dian/documents/{id}/xml` | GET | Disponible |
| **PDF de un documento** | `/api/co/dian/documents/{id}/pdf` | GET | Disponible |
| **Enviar el documento por correo (con PDF propio)** | `/api/co/dian/documents/{id}/send` | POST | Disponible |
| **Resoluciones del emisor** | `/api/co/dian/resolutions` | GET | Disponible |
| **Sincronizar resoluciones (emisores Dataico)** | `/api/co/dian/resolutions/sync` | POST | Disponible |
| **Nota Crédito** | `/api/co/dian/credit-notes` | POST | Disponible (contrato nuevo) |
| **Nota Débito** | `/api/co/dian/debit-notes` | POST | Disponible (contrato nuevo) |
| **Nómina Electrónica (emisión)** | `/api/co/dian/payroll` | POST | Disponible |
| **Nómina Electrónica (anulación)** | `/api/co/dian/payroll/void` | POST | Disponible |
| **Documento Soporte (emisión)** | `/api/co/dian/support-documents` | POST | Disponible |
| **Documento Soporte (nota de ajuste)** | `/api/co/dian/support-documents/adjustment` | POST | Disponible |
| **Doc. Equivalente - POS** | `/api/co/dian/equivalent-documents/pos` | POST | Disponible |
| **Doc. Equivalente - Cine** | `/api/co/dian/equivalent-documents/cine` | POST | Disponible |
| **Doc. Equivalente - Espectáculos públicos** | `/api/co/dian/equivalent-documents/espectaculos` | POST | Disponible |
| **Doc. Equivalente - Juegos localizados** | `/api/co/dian/equivalent-documents/juegos-localizados` | POST | Disponible |
| **Doc. Equivalente - Transporte terrestre** | `/api/co/dian/equivalent-documents/transporte-terrestre` | POST | Disponible |
| **Doc. Equivalente - Peajes** | `/api/co/dian/equivalent-documents/peajes` | POST | Disponible |
| **Doc. Equivalente - Extracto** | `/api/co/dian/equivalent-documents/extracto` | POST | Disponible* |
| **Doc. Equivalente - Transporte aéreo** | `/api/co/dian/equivalent-documents/transporte-aereo` | POST | Disponible |
| **Doc. Equivalente - Bolsa de Valores/Agro** | `/api/co/dian/equivalent-documents/bolsa` | POST | Disponible |
| **Doc. Equivalente - Servicios públicos** | `/api/co/dian/equivalent-documents/servicios-publicos` | POST | Disponible* |

| **Factura del sector salud (con RIPS)** | `/api/co/dian/health-invoices` | POST | Disponible (solo emisión directa) |
| **Validar sin emitir (salud)** | `/api/co/dian/health-invoices/validate` | POST | Disponible |
| **RIPS de una factura de salud (enviar / reenviar)** | `/api/co/dian/health-invoices/{id}/rips` | POST | Disponible |
| **RIPS de una factura de salud (estado y CUV)** | `/api/co/dian/health-invoices/{id}/rips` | GET | Disponible |
| **Sector Transporte** | `/api/co/dian/transport-invoices` | POST | Disponible |
| **Facturas recibidas (RADIAN)** | `/api/co/dian/received-documents` | GET, POST | Disponible |
| **Eventos RADIAN de una factura recibida** | `/api/co/dian/received-documents/{id}/events` y `/events/{030\|031\|032\|033}` | GET, POST | Disponible |
| **RIPS independiente de factura** | `/api/co/minsalud/rips/emit` | POST | Disponible |
| **Consulta de adquirente (nombre y correo por documento)** | `/api/co/dian/acquirers` | POST | Disponible |

\* El literal exacto de `ProfileID` para Extracto y Servicios Públicos se reconstruyó a partir de la descripción del catálogo — no se pudo confirmar carácter por carácter contra el anexo por un salto de línea en el PDF. Conviene validarlo contra el set de pruebas de habilitación antes de producción real.

Los demás tipos de documento (notas, nómina, soporte, equivalentes, transporte) conservan por ahora su estructura anterior y se migran al contrato nuevo por fases: mientras tanto devuelven un `TrackingId` que la consulta por UUID todavía no resuelve. El contrato de la factura de venta, las resoluciones y la consulta es el vigente.

Los endpoints marcados "Pendiente" responden `501 Not Implemented` y no aparecen en el Swagger
público — se activan a medida que se construye su generación UBL propia.

---

## 4. Estructuras JSON Esperadas (Payloads)

A continuación, se presentan los esqueletos simplificados de los JSON que espera cada endpoint. 
*(Nota: El catálogo completo de catálogos paramétricos DIAN -ciudades, impuestos, unidades de medida- se encuentra en el Anexo Técnico V1.9).*

### 4.1 Factura de Venta (`POST /api/co/dian/invoices`)

El JSON tiene la misma estructura que usa Dataico y puede enviarse directo o dentro de `{ "invoice": { ... } }`. El emisor sale de la API Key y los totales los calcula el servidor: no se envían.

```json
{
  "number": "1001",
  "issue_date": "01/11/2026",
  "payment_means_type": "DEBITO",
  "payment_means": "CASH",
  "numbering": { "prefix": "SETT", "resolution_number": "18760000001" },
  "customer": {
    "party_identification_type": "NIT",
    "party_identification": "900123456",
    "company_name": "ACME SAS",
    "email": "facturas@acme.co",
    "address_line": "Calle 10 # 5-20",
    "city": "BOGOTA, D.C."
  },
  "items": [
    {
      "sku": "A1",
      "description": "Producto de ejemplo",
      "quantity": 2,
      "price": 10000,
      "taxes": [{ "tax_category": "IVA", "tax_rate": 19 }]
    }
  ],
  "notes": ["Gracias por su compra"]
}
```

Reglas principales:

- `number` es obligatorio, entero positivo, dentro del rango de la resolución. `numbering.prefix` identifica la resolución; `resolution_number` es opcional si el prefijo es único.
- `payment_means_type`: `DEBITO` (contado, exige `payment_means`) o `CREDITO` (exige `payment_date`). Fechas en `dd/MM/yyyy`.
- IVA: una línea con `tax_category: "IVA"` y tarifa mayor que 0 es gravada; con tarifa 0 es exenta; sin IVA es excluida. Retenciones en `retentions` por línea.
- La dirección es flexible: el municipio se ubica por código DANE (`department` + `city`) o por nombre. Si no se logra ubicar, la dirección se omite y la respuesta lo avisa en `warnings`.
- Mandato: `operation: "MANDATO"` y el mandante en cada línea. Exportación: `invoice_type_code: "FACTURA_EXPORTACION"` y moneda extranjera habilitada para el emisor.
- AIU: se envía el bloque `aiu` en lugar de `items`; el servidor genera las líneas de Administración, Imprevistos y Utilidad. El valor del servicio va en `aiu.base_amount` (un solo valor) o en `aiu.items` (varias líneas de servicio, con la misma forma que `items`), nunca en ambos. Los porcentajes (`administration_percent`, `unforeseen_percent`, `utility_percent`, `utility_iva_rate`, `service_iva_rate`) van entre 0 y 100. El objeto del contrato es opcional.
- Impuestos de la línea: ver la sección «Impuestos de la línea» más abajo.
- La fecha efectiva del documento ante la DIAN es la del momento de la emisión; `issue_date` es informativa.

#### Impuestos de la línea

Cada línea lleva sus impuestos en `taxes` y sus retenciones en `retentions`. El servidor calcula las bases y los valores; el integrador solo envía la categoría y la tarifa.

| Categoría (`tax_category`) | Qué es | Cómo se envía | Estado |
|---|---|---|---|
| `IVA` | Impuesto sobre las ventas | `tax_rate` (porcentaje). 0 es exento; sin la línea de IVA, excluido | Disponible |
| `IMP_CONSUMO` | Impuesto nacional al consumo (INC, tributo 04 del anexo técnico) | `tax_rate` (porcentaje) | Disponible. La base es la de la línea después del descuento y el valor se suma al total de la línea y del documento |
| `IMP_CONSUMO_LICOR` | Consumo de licores | `tax_rate` | En revisión: su código ante la DIAN (ICL, 32) se está confirmando; no lo uses en producción todavía |
| `IMP_BOLSA_PLASTICA` | Impuesto a la bolsa plástica (INC Bolsas, 22) | `tax_rate` | En revisión: igual que el anterior. El valor fijo por unidad aún no se admite |

Reglas:

- La categoría debe estar en el catálogo activo del sistema. Con una categoría desconocida la respuesta es `400 VALIDATION_ERROR` e incluye la lista de las aceptadas.
- `tax_rate` va entre 0 y 100. Un impuesto con valor fijo (`tax_amount`, por ejemplo la bolsa o el ICUI) se rechaza hoy con un mensaje claro.
- El texto de la categoría se normaliza a mayúsculas. Se usa el nombre de la tabla, no el código numérico del anexo técnico (04, 22...): el sistema hace esa traducción al armar el documento.
- Las retenciones (`RET_FUENTE`, `RET_ICA`, `RET_IVA`) van en `retentions` con `tax_rate`.

Pendiente de definir, y se documentará aquí cuando exista: el resto de los tributos del anexo (IBUA, ICUI, combustibles, carbono, timbre, impuestos al consumo departamentales y otros), el valor fijo por unidad (`tax_amount`) y la cantidad base de los tributos que se calculan por contenido.

### 4.2 Nota Crédito y Nota Débito (`POST /api/co/dian/credit-notes` y `/debit-notes`)

Se emiten sobre una factura **aprobada** del mismo emisor. El cliente, la resolución y el medio de pago salen de la factura; la respuesta es síncrona, igual que la factura. Puede enviarse directo o dentro de `{ "credit_note": { ... } }` / `{ "debit_note": { ... } }`.

Nota crédito total (anula la factura: sin `items` copia sus líneas):
```json
{
  "invoice_number": "SFRY1005",
  "number": "1",
  "reason": "ANULACION",
  "reason_description": "Anulación de la factura de prueba"
}
```

Nota crédito parcial o nota débito (con `items`, que lleva el valor del ajuste):
```json
{
  "invoice_id": "6f1f6f7e-3b0a-4c1d-9a64-0b6d1c9f2a10",
  "number": "2",
  "numbering": { "prefix": "NCSF" },
  "reason": "INTERESES",
  "items": [
    { "description": "Intereses de mora", "quantity": 1, "price": 5000 }
  ]
}
```

Reglas principales:

- La factura se indica con `invoice_id` (el `id` que devolvió la emisión, o el id de Dataico) o con `invoice_number` (prefijo y número, por ejemplo `SFRY1005`). Si no existe para tu emisor responde `404 INVOICE_NOT_FOUND`; si no está aprobada, `422 INVOICE_NOT_APPROVED`.
- `number` es tu consecutivo de notas, sin prefijo. Las notas no tienen rango autorizado por la DIAN: solo se valida que ese prefijo y número no estén usados (`409 DOCUMENT_ALREADY_EXISTS` con la información del documento existente). Sin `numbering.prefix` se usa el prefijo de notas del emisor.
- `reason` (obligatorio). Nota crédito: `DEVOLUCION`, `ANULACION` u `OTROS`. Nota débito: `INTERESES`, `GASTOS_POR_COBRAR`, `CAMBIO_DEL_VALOR` u `OTROS`. También se acepta el código DIAN (1 a 6 en crédito, 1 a 4 en débito). `reason_description` es opcional: es un texto libre que se agrega a las observaciones de la nota (la descripción oficial del motivo la pone el sistema, porque la DIAN la valida contra su lista).
- Nota crédito sin `items`: es total. Con `items`: es parcial y el servidor calcula los totales. La suma de las notas crédito aprobadas no puede superar el total de la factura (`422 NOTE_EXCEEDS_INVOICE`).
- La nota débito siempre necesita `items`.
- **Nota crédito sin factura de referencia:** si no envías `invoice_id` ni `invoice_number`, la nota no afecta a ninguna factura. Entonces son obligatorios `customer` (con `party_identification_type` y `party_identification`, igual que en la factura), `items` y `reason`. `payment_means_type` y `payment_means` son opcionales (por defecto contado en efectivo). Usa la resolución de factura vigente de la sucursal (la predeterminada, o la que indiques en `numbering.resolution_number`) solo para identificar al emisor: el consecutivo es el de notas. El período que se informa a la DIAN es el mes de la emisión, y no aplica el tope de `NOTE_EXCEEDS_INVOICE`. La nota débito sin factura todavía no está disponible (`422 UNREFERENCED_DEBIT_NOT_AVAILABLE`).
- Los `items` tienen la misma estructura de la factura (`description`, `quantity`, `price`, `taxes`, `discount_rate`...).
- El contador de notas del portal avanza más allá del número que uses, para que el portal no lo repita.

### 4.3 Documento Equivalente POS (`/api/co/dian/equivalent-documents/pos`)

Tiquete de máquina registradora, emitido directo a la DIAN con el certificado del emisor. Es el mismo JSON de la factura (4.1) más el bloque `pos` con los datos de la caja; requiere una resolución activa de documento equivalente POS. Sin `customer` el tiquete va a nombre del consumidor final.

```json
{
  "number": "10045",
  "issue_date": "21/05/2026",
  "payment_means_type": "DEBITO",
  "payment_means": "EFECTIVO",
  "numbering": { "prefix": "POS" },
  "items": [ { "sku": "TAT01", "description": "Tatuaje manga - sesión 1", "quantity": 1, "price": 300000 } ],
  "pos": {
    "box_plate": "CAJA-01",
    "box_location": "ALMACEN",
    "cashier": "Maria Perez",
    "box_type": "Registradora",
    "sale_code": "V-10045"
  }
}
```

`box_plate`, `box_location`, `cashier`, `box_type` y `sale_code` son obligatorios (hasta 100 caracteres cada uno). Los beneficios del comprador (`buyer_code`, `buyer_name`, `buyer_points`) son opcionales, pero van completos o no van.

### 4.4 Factura del sector salud con RIPS (`/api/co/dian/health-invoices`)

Factura electrónica de venta de un prestador (PSS) o proveedor de tecnologías en salud (PTS) a una entidad responsable de pago, con el RIPS como soporte (Resoluciones 2275 de 2023 y 948 de 2026, Documento Técnico 2 del Ministerio de Salud). La plataforma emite la factura a la DIAN con la extensión de salud y, ya aprobada, envía el RIPS al Mecanismo Único de Validación del Ministerio, que devuelve el **CUV**.

> **Solo emisión directa a la DIAN.** Un emisor que usa otro integrador recibe `422 HEALTH_NATIVE_ONLY`. El emisor debe tener, además, sus credenciales de prestador ante el MUV cargadas en el portal (ambiente de pruebas y de producción por separado).

Es el mismo JSON de la factura (4.1) más el bloque `health` y, opcional, el bloque `rips`. El endpoint fija `operation` en `SALUD`; no lo envíes.

```json
{
  "number": "1001",
  "issue_date": "01/11/2026",
  "payment_means_type": "DEBITO",
  "payment_means": "EFECTIVO",
  "numbering": { "prefix": "SETT", "resolution_number": "18760000001" },
  "customer": { "party_identification_type": "NIT", "party_identification": "800111222", "company_name": "EPS EJEMPLO SA", "party_type": "1" },
  "items": [ { "sku": "CONS01", "description": "Consulta médica general", "quantity": 1, "price": 38000 } ],
  "health": {
    "operation_type": "SS-CUFE",
    "provider_code": "1100100001",
    "payment_modality": "04",
    "coverage": "01",
    "contract_number": "CUCON-2026-0001",
    "period_start": "01/11/2026",
    "period_end": "01/11/2026",
    "copayment": 2000,
    "moderator_fee": 3000
  },
  "rips": {
    "numDocumentoIdObligado": "900303194",
    "numFactura": "SETT1001",
    "tipoNota": null,
    "numNota": null,
    "usuarios": [
      {
        "consecutivo": 1,
        "tipoDocumentoIdentificacion": "CC",
        "numDocumentoIdentificacion": "1020304050",
        "tipoUsuario": "01",
        "fechaNacimiento": "1990-05-14",
        "codSexo": "F",
        "codPaisResidencia": "170",
        "codMunicipioResidencia": "11001",
        "codZonaTerritorialResidencia": "01",
        "incapacidad": "NO",
        "codPaisOrigen": "170",
        "servicios": {
          "consultas": [
            {
              "codPrestador": "110010000101",
              "fechaInicioAtencion": "2026-11-01 09:30",
              "codConsulta": "890201",
              "vrServicio": 38000,
              "conceptoRecaudo": "01",
              "valorPagoModerador": 2000,
              "consecutivo": 1
            }
          ]
        }
      }
    ]
  }
}
```

(El ejemplo del RIPS está abreviado: cada servicio lleva todos los campos de la Resolución 2275 de 2023.)

**Bloque `health`**

| Campo | Obligatorio | Descripción |
| :--- | :--- | :--- |
| `operation_type` | No | Tipo de operación: `SS-CUFE` (por defecto), `SS-CUDE`, `SS-POS`, `SS-SNum`, `SS-Reporte`, `SS-SinAporte` o `SS-Recaudo`. Los cuatro primeros acreditan recaudos de otras facturas y llevan la extensión de salud. |
| `provider_code` | Sí | Código de habilitación del prestador, 10 dígitos (tabla `IPSCodHabilitacion` de SISPRO). En el RIPS el `codPrestador` lleva 12: estos 10 más 2 de la sede. |
| `payment_modality` | Sí | Tabla `modalidadPago`: `01` pago individual por caso / conjunto integral / paquete / canasta, `02` pago global prospectivo, `03` capitación, `04` evento. |
| `coverage` | Sí | Tabla `coberturaPlan`: `01` plan de beneficios con UPC, `02` presupuesto máximo, `03` prima EPS/EOC, `04` póliza SOAT, `05` ARL, `06` ADRES, `07` salud pública, `08` entidad territorial, `09` urgencias población migrante, `10` plan complementario, `11` medicina prepagada, `12` otras pólizas en salud, `13` régimen especial o excepción, `14` población privada de la libertad, `15` particular. |
| `contract_number` | Uno de los tres | Número de contrato (código CUCON del módulo de contratos de SIIFA). No se envía junto con la póliza. |
| `policy_number` | Uno de los tres | Número de póliza; solo con las coberturas `04` y `12`. |
| `no_contract_reason` | Uno de los tres | Cuando no hay contrato ni póliza: tabla `facturaSinContrato` (`01` urgencias, `02` a cargo de ADRES o aseguradora SOAT / planes voluntarios, `03` tutela u orden judicial, `04` portabilidad o asignación masiva, `05` caso excepcional sin contrato, `06` recuperación de órganos para trasplante, `07` pacientes particulares). |
| `period_start`, `period_end` | Sí | Período de prestación de los servicios, `dd/MM/yyyy`. |
| `copayment`, `moderator_fee`, `shared_payments`, `advance` | No | Recaudos del usuario ya cobrados, uno por concepto. Son informativos: no restan del total de la factura ni pueden superarlo. Copago, cuota moderadora y pagos compartidos deben coincidir exactamente con la suma de `valorPagoModerador` por `conceptoRecaudo` (`01`, `02`, `03`) del RIPS; el anticipo solo se reporta en la factura. |
| `collection_date` | No | Fecha del último recaudo; por defecto la de emisión. |

**Qué valida la plataforma antes de emitir** (`400 VALIDATION_ERROR` por el bloque `health`, `422 RIPS_INVALID` por el RIPS; en ambos casos **no se emite nada**):

- Los códigos de las tablas anteriores, el largo de `provider_code`, que contrato y póliza no vayan juntos, y las fechas.
- RIPS: `numDocumentoIdObligado` igual al NIT del emisor sin dígito de verificación; `numFactura` igual al número de la factura (prefijo + número); `tipoNota` y `numNota` vacíos; usuarios con `consecutivo` único, documento y fecha de nacimiento (`yyyy-MM-dd`); `codPrestador` de 12 dígitos cuyos 10 primeros coinciden con `provider_code`; `conceptoRecaudo` válido y presente cuando hay valor recaudado; y que los recaudos del RIPS sumen lo mismo que los de la factura.
- Avisos que no bloquean (vienen en `warnings`): un tipo de documento fuera de la lista conocida de SISPRO, o que la suma de `vrServicio` difiera del total de la factura.
- Lo que el Ministerio valida por su cuenta y aquí no se repite: las reglas clínicas (diagnóstico contra sexo y edad) y el cruce de la cobertura con el tipo de usuario. Esos rechazos llegan como glosas.

**Respuesta (`201`).** La misma de la factura (secciones 4.1 y 5) más el estado del RIPS en `rips`:

```json
{
  "id": "6f1f6f7e-3b0a-4c1d-9a64-0b6d1c9f2a10",
  "number": "SETT1001",
  "status": "APPROVED",
  "cufe": "3a4b5c6d...",
  "rips": {
    "status": "ACCEPTED",
    "cuv": "0e6dd9f85990e6fbc77e37f57e89876ab117026daa2baaf2ad667f44482fbf4a26aab4314f51af12535aa053adbe6dfd",
    "processId": 1254,
    "message": "CUV generado por el Mecanismo Único de Validación.",
    "attempts": 1,
    "findings": [],
    "warnings": []
  }
}
```

`rips.status`: `ACCEPTED` (CUV entregado), `REJECTED` (el Ministerio lo rechazó; `findings` trae cada glosa con `class`, `code`, `description`, `notes` y `source`), `PENDING` (no hubo veredicto: falló la conexión o falta el archivo de la DIAN; se puede reintentar) o `NOT_SENT` (la factura no trajo RIPS, o no quedó aprobada). **Que el RIPS falle no deshace la factura:** la factura ya está emitida y aprobada; se corrige el RIPS y se reenvía.

**Validar sin emitir** — `POST /api/co/dian/health-invoices/validate`. Mismo cuerpo de la emisión (con `health` y `rips`); además acepta `total` (valor de la factura, para contrastar los servicios). Responde `200 { "valid": true|false, "errors": [...], "warnings": [...] }`. No crea ningún documento.

**Enviar o reenviar el RIPS** — `POST /api/co/dian/health-invoices/{id}/rips`, con `{ "rips": { ... } }` (reemplaza al guardado) o sin cuerpo (reenvía el último). La factura debe estar aprobada.

| Código | Cuándo |
| :--- | :--- |
| `200` | El Ministerio aceptó el RIPS; trae el CUV. Si ya estaba aceptado, devuelve el CUV sin volver a enviar. |
| `400 VALIDATION_ERROR` | La factura no tiene RIPS guardado y no enviaste uno. |
| `404 DOCUMENT_NOT_FOUND` | No hay una factura de salud con ese `id` en tu emisor. |
| `409 DOCUMENT_NOT_APPROVED` / `NOT_A_HEALTH_INVOICE` | La factura aún no está aprobada, o no tiene datos de salud. |
| `422 RIPS_INVALID` | El RIPS no cuadra con la factura (`details`). |
| `422 RIPS_REJECTED` | El Ministerio lo rechazó; `rips.findings` trae las glosas. |
| `502 MUV_UNAVAILABLE` | No se pudo contactar al Ministerio; reintenta con el mismo cuerpo. |

**Consultar el RIPS** — `GET /api/co/dian/health-invoices/{id}/rips` devuelve el mismo bloque `rips` de arriba (`404 RIPS_NOT_FOUND` si la factura no tiene).

**Recaudo de cuotas moderadoras y copagos (`SS-Recaudo`).** El prestador factura el recaudo de forma individual a cada usuario, con `operation_type: "SS-Recaudo"`. Esa factura **no lleva la extensión de salud** ni se envía a la entidad responsable de pago; se acredita después en la factura de cobro con los campos `copayment` o `moderator_fee`. Aún no se emite el bloque del usuario (`Person`) que el Documento Técnico 2 prevé para estas facturas.

**Fuera de esta primera entrega.** Las notas crédito y débito de salud (tipos de operación 20 y 30, con el período de facturación) y los RIPS de nota, capitación y ajuste. El RIPS sin factura de por medio sigue por `/api/co/minsalud/rips/emit` (disponible hoy).

### 4.5 Sector Transporte de Carga (RNDC) (`/api/co/dian/transport-invoices`) — Pendiente (501)
Facturación con los requisitos especiales del Ministerio de Transporte y DIAN.
```json
{
  "prefix": "TRANS",
  "documentNumber": "740",
  "transportDetails": {
    "radicacionRemesa": "RNDC123456789",
    "valorFlete": 1500000.00,
    "placaVehiculo": "XYZ-999"
  },
  "lines": [ ... ],
  "totalAmount": 1500000.00
}
```

### 4.6 Consulta de adquirente (`POST /api/co/dian/acquirers`)
Con el tipo y el número de documento del comprador devuelve el nombre o razón social y los correos de recepción de factura que registró ante la DIAN (servicio de la Resolución 202 de 2025). La consulta la firma la plataforma con el certificado del emisor autenticado: tu software no necesita el certificado.

El documento va en el cuerpo JSON y no en la URL, para que no quede en las bitácoras del servidor:

```json
{ "identificationType": "31", "identificationNumber": "900123456" }
```

- `identificationType`: código DIAN — 11, 12, 13, 21, 22, 31 (NIT), 41, 42, 47, 48, 50 o 91.
- `identificationNumber`: entre 3 y 20 letras o dígitos, sin puntos, guiones ni dígito de verificación.
- Solo para emisores con emisión directa a la DIAN (NATIVE) y certificado activo; usa el ambiente (habilitación o producción) del emisor.
- La DIAN permite esta consulta únicamente al momento de la venta y prohíbe su uso masivo y distribuir los datos: el API limita a 30 consultas por minuto por emisor (`429`) y registra quién consulta. No guardes los resultados fuera del tercero de tu cliente.
- Si el adquirente no aparece (`found: false`) hay que pedirle sus datos. La fuente de la DIAN es lo que cada adquirente registró; no todos están.

```json
{
  "identificationType": "31",
  "identificationNumber": "900123456",
  "found": true,
  "name": "COMERCIALIZADORA EJEMPLO S.A.S.",
  "emails": ["facturacion@ejemplo.com"],
  "message": "OK"
}
```

### 4.7 Facturas recibidas y eventos RADIAN (`/api/co/dian/received-documents`)
Son las facturas que tus proveedores le emitieron a tu empresa. Registrar sobre ellas el acuse, el recibo del bien y la aceptación (o el reclamo) es lo que las convierte en título valor y las hace deducibles. Es el mismo motor del portal: el evento lo firma y envía la plataforma con el certificado del emisor (emisión directa) o por Dataico, según cómo esté configurado. Se ven los documentos de la sucursal dueña de la llave.

**Ingresar una factura recibida** — `POST /api/co/dian/received-documents`

```json
{ "file": "<XML o ZIP en Base64>", "fileName": "factura.zip" }
```

- `file`: el XML de la factura, o el ZIP que manda el proveedor (con el XML y, si lo trae, el PDF). También se acepta el `AttachedDocument` de la DIAN. `fileName` es opcional.
- Responde `201` con el `id`, el CUFE, el proveedor, el total y `isCredit`. Si el emisor tiene eventos configurados para enviarse solos, se disparan en esa misma llamada y vienen en `events`.
- `409 DUPLICATE_RECEIVED_DOCUMENT` si ese CUFE ya estaba ingresado (trae el `id` existente). `400` si el archivo no se puede leer, no es Invoice/CreditNote/DebitNote o es un documento soporte.

**Listar** — `GET /api/co/dian/received-documents?q=&cufe=&page=&pageSize=`: del más reciente al más antiguo, con el estado de cada evento. `q` busca por proveedor, NIT o número (todas las palabras, en cualquier orden); `cufe` trae uno exacto. `pageSize` máximo 100.

**Registrar un evento** — `POST /api/co/dian/received-documents/{id}/events/{código}`, sin cuerpo:

| Código | Evento |
|---|---|
| 030 | Acuse de recibo |
| 031 | Reclamo |
| 032 | Recibo del bien o servicio |
| 033 | Aceptación expresa |

- Solo aplican a facturas **a crédito** (`isCredit: true`); en otra forma de pago responde `422 NOT_CREDIT_INVOICE`.
- Orden que exige la DIAN: el 030 primero; el 032 después del 030; el 033 y el 031 después de ambos, y son excluyentes entre sí. Fuera de orden responde `422 EVENT_REJECTED` sin enviar nada a la DIAN ni gastar consecutivo.
- Un evento ya aceptado no se reenvía: la llamada devuelve el mismo (`200`). Un rechazo sí se puede reintentar.
- `200` con `status` `SENT` (y el `cude` del evento) o `DUPLICATE`; `409 EVENT_IN_PROGRESS` si ese evento se está procesando; `422 INVALID_EVENT_CODE` / `EVENT_REJECTED` con el motivo en `message`.

**Historial** — `GET /api/co/dian/received-documents/{id}/events`: cada intento con `id`, `code`, `status`, `cude`, `number` (consecutivo, solo Dataico), `message` (respuesta de la DIAN) y `sentAt`.

**XML del evento** — `GET /api/co/dian/received-documents/{id}/events/{eventId}/file`, con el `id` del evento: devuelve el XML que se envió a la DIAN (`application/xml`). Con emisión directa es el que firma la plataforma, guardado desde este cambio (los eventos anteriores responden `404 EVENT_FILE_NOT_AVAILABLE`); con Dataico se baja del proveedor (`502` si no lo permite). Los eventos no tienen PDF.

Hoy el reclamo (031) se envía sin concepto de reclamo y la aceptación tácita (034) no se envía desde la plataforma.

---

## 5. Respuestas del Sistema

**Emisión exitosa (`201 Created`):**
```json
{
  "id": "6f1f6f7e-3b0a-4c1d-9a64-0b6d1c9f2a10",
  "type": "FE-STD",
  "number": "SETT1001",
  "issueDate": "2026-11-01T10:30:00",
  "total": 23800.00,
  "status": "APPROVED",
  "cufe": "3a4b5c6d...",
  "dianResponse": "Documento validado por la DIAN",
  "warnings": []
}
```

**Número ya usado (`409 DOCUMENT_ALREADY_EXISTS`):** trae `id`, `type`, `number`, `issueDate`, `total`, `status`, `cufe` y `dianResponse` del documento existente.

**Consulta (`GET /api/co/dian/documents/{id}`):** `id`, `type`, `number`, `issueDate`, `total`, `status`, `dianResponse`, `cufe` y `files`.

**Resoluciones (`GET /api/co/dian/resolutions`):** objeto con dos bloques. `resolutions` es un arreglo con `documentType`, `prefix`, `resolutionNumber`, `numberStart`, `numberEnd`, `nextNumber` (solo referencia), `validFrom`, `validTo` y `expired`; acepta `?documentType=FE`. `noteNumberings` (siempre completo, sin filtro) trae las numeraciones de las notas crédito, débito y de ajuste de la sucursal: `kind` (`CREDIT_NOTE`, `DEBIT_NOTE`, `SUPPORT_ADJUSTMENT`), `scope` (`branch` propia de la sucursal, `client` del emisor o `shared` compartida), `prefix` (si la numeración no tiene uno propio, el de su resolución), `nextNumber`, `isDefault` y `resolutionNumber`.
