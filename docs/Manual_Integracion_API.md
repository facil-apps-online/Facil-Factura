# Manual de Integración B2B - Facil-Factura.pro API

Bienvenido al manual de integración técnica para la emisión de Documentos Electrónicos (DIAN y MinSalud) a través de Facil-Factura.pro. Esta API RESTful está diseñada para procesar altos volúmenes de documentos de forma asíncrona, garantizando tiempos de respuesta instantáneos.

## 1. Seguridad y Autenticación (HMAC)

Para garantizar la integridad y el origen de las peticiones, la API utiliza un esquema de seguridad basado en firmas HMAC SHA-256 y control de tasa (Rate Limiting de 100 req/seg).

Todas las peticiones `POST` y `GET` a la API deben incluir los siguientes **Headers**:

*   `x-api-key`: Tu llave pública (identificador del Tenant/Empresa).
*   `x-api-timestamp`: Marca de tiempo UNIX actual en segundos (ej. `1716301234`). Prevención contra Replay Attacks.
*   `x-api-signature`: Firma HMAC calculada sobre el cuerpo de la petición.

### ¿Cómo calcular el `x-api-signature`?
Debes concatenar el timestamp, un punto `.` y el cuerpo (JSON) exacto de la petición, y luego hashearlo usando tu `API_SECRET` (Llave privada).

**Ejemplo en C#:**
```csharp
string payload = $"{timestamp}.{jsonBody}";
using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
{
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    string signature = Convert.ToBase64String(hash);
}
```

---

## 2. Flujo Asíncrono (Emisión y Consulta)

Dado que la DIAN puede presentar intermitencias, nuestro sistema encola tus documentos y los procesa en segundo plano.

1.  **Emisión:** Envías el JSON al endpoint correspondiente. La API te responderá inmediatamente un `202 Accepted` con un `TrackingId`.
2.  **Consulta (Polling):** Haces un `GET` a `/api/co/dian/documents/{TrackingId}/status` para verificar si la DIAN ya aceptó el documento y obtener el CUFE.
3.  **Descarga:** Haces un `GET` a `/api/co/dian/documents/{TrackingId}/files` para obtener los Base64 del XML oficial y el PDF.

---

## 3. Catálogo de Endpoints

URL Base: `https://api.facil-factura.pro` (Sujeto a ambiente QA/PROD)

Las rutas llevan el prefijo `co/dian` (país/autoridad regulatoria) para poder sumar otros países sin
romper las existentes — ej. mañana `api/mx/sat/invoices`.

| Documento | Endpoint | Método | Estado |
| :--- | :--- | :--- | :--- |
| **Factura de Venta** | `/api/co/dian/invoices` | POST | Disponible |
| **Nota Crédito** | `/api/co/dian/credit-notes` | POST | Disponible |
| **Nota Débito** | `/api/co/dian/debit-notes` | POST | Disponible |
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

Los endpoints marcados "Pendiente" responden `501 Not Implemented` y no aparecen en el Swagger
público — se activan a medida que se construye su generación UBL propia.

---

## 4. Estructuras JSON Esperadas (Payloads)

A continuación, se presentan los esqueletos simplificados de los JSON que espera cada endpoint. 
*(Nota: El catálogo completo de catálogos paramétricos DIAN -ciudades, impuestos, unidades de medida- se encuentra en el Anexo Técnico V1.9).*

### 4.1 Factura Electrónica Estándar (`/api/co/dian/invoices`)
```json
{
  "prefix": "SETT",
  "documentNumber": "990001",
  "issueDate": "2024-05-21T10:30:00Z",
  "currencyCode": "COP",
  "customer": {
    "identificationType": "31",
    "identificationNumber": "900123456",
    "name": "Cliente de Ejemplo SAS",
    "email": "facturacion@cliente.com"
  },
  "lines": [
    {
      "id": "1",
      "description": "Desarrollo de Software a la medida",
      "quantity": 1,
      "price": 5000000.00,
      "taxes": [
        {
          "taxCode": "01",
          "taxPercent": 19.00,
          "taxAmount": 950000.00
        }
      ]
    }
  ],
  "totalAmount": 5950000.00
}
```

### 4.2 Nota Crédito (`/api/co/dian/credit-notes`) — Pendiente (501)
Requiere referenciar obligatoriamente el CUFE de la factura original.
```json
{
  "prefix": "NC",
  "documentNumber": "105",
  "issueDate": "2024-05-22T08:00:00Z",
  "billingReference": {
    "invoiceNumber": "SETT990001",
    "uuid": "a1b2c3d4e5f6g7h8i9j0..." // CUFE de la factura afectada
  },
  "discrepancyResponse": {
    "referenceId": "SETT990001",
    "responseCode": "2", // 2 = Anulación de factura electrónica
    "description": "Anulación por error en los montos facturados"
  },
  "lines": [ ... ],
  "totalAmount": 5950000.00
}
```

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

---

## 5. Respuestas del Sistema

**Al Enviar un Documento (202 Accepted):**
```json
{
  "message": "Documento Equivalente POS recibido y encolado para procesamiento.",
  "trackingId": "POS-10045",
  "status": "PENDING"
}
```

**Al Consultar Estado (GET /api/co/dian/documents/{TrackingId}/status):**
```json
{
  "trackId": "POS-10045",
  "status": "ACCEPTED",
  "dianResponse": "Procesado Correctamente",
  "cufe": "3a4b5c6d...",
  "filesUrl": "/api/co/dian/documents/POS-10045/files"
}
```
