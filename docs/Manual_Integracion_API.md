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
3.  **Descarga:** `GET /api/co/dian/documents/{id}/files` entrega el XML firmado, la respuesta de la DIAN y el AttachedDocument en Base64 real (`/xml` descarga el XML directo). El PDF (`/pdf`) llega en una entrega posterior.

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

\* El literal exacto de `ProfileID` para Extracto y Servicios Públicos se reconstruyó a partir de la descripción del catálogo — no se pudo confirmar carácter por carácter contra el anexo por un salto de línea en el PDF. Conviene validarlo contra el set de pruebas de habilitación antes de producción real.
| **Sector Salud (RIPS, con factura)** | `/api/co/dian/health-invoices/rips` | POST | Pendiente (501) |
| **Sector Transporte** | `/api/co/dian/transport-invoices` | POST | Disponible |
| **Eventos (Acuses/Radian)**| `/api/co/dian/reception-events` | POST | Pendiente (501) |
| **RIPS independiente de factura** | `/api/co/minsalud/rips/emit` | POST | Disponible |
| **Consulta de adquirente (nombre y correo por documento)** | `/api/co/dian/acquirers` | POST | Disponible |

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
- Aún no se admiten AIU (responde `422 OPERATION_NOT_SUPPORTED`) ni impuestos de valor fijo por unidad (`tax_amount`, como la bolsa), que se rechazan con un mensaje claro.
- La fecha efectiva del documento ante la DIAN es la del momento de la emisión; `issue_date` es informativa.

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
- Los `items` tienen la misma estructura de la factura (`description`, `quantity`, `price`, `taxes`, `discount_rate`...).
- El contador de notas del portal avanza más allá del número que uses, para que el portal no lo repita.

### 4.3 Documento Equivalente POS (`/api/co/dian/equivalent-documents/pos`) — Pendiente (501)
```json
{
  "prefix": "POS",
  "documentNumber": "10045",
  "issueDate": "2024-05-21T15:45:00Z",
  "posPointOfSaleId": "CAJA-01",
  "hardwareId": "TERM-99",
  "buyer": {
    "isConsumer": true, // Consumidor Final (222222222222)
    "identificationNumber": "222222222222"
  },
  "lines": [
    {
      "description": "Tatuaje Manga - Sesión 1",
      "quantity": 1,
      "price": 300000.00
    }
  ],
  "totalAmount": 300000.00
}
```

### 4.4 RIPS Sector Salud, con factura (`/api/co/dian/health-invoices/rips`) — Pendiente (501)

> Para RIPS sin factura de por medio, ve directo a MinSalud vía `/api/co/minsalud/rips/emit` (Fel.Api.Tenant), disponible hoy.
Además de la factura, incluye la data clínica obligatoria del Ministerio de Salud.
```json
{
  "prefix": "SALUD",
  "documentNumber": "850",
  "healthData": {
    "providerCode": "0500112345", // Código de habilitación IPS
    "epsCode": "EPS001",
    "consultations": [
      {
        "patientId": "1010101010",
        "diagnosisCode": "Z000",
        "consultationPurpose": "10"
      }
    ]
  },
  "lines": [ ... ]
}
```

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
