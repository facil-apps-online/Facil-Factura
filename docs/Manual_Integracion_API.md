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
2.  **Consulta (Polling):** Haces un `GET` a `/api/documents/{TrackingId}/status` para verificar si la DIAN ya aceptó el documento y obtener el CUFE.
3.  **Descarga:** Haces un `GET` a `/api/documents/{TrackingId}/files` para obtener los Base64 del XML oficial y el PDF.

---

## 3. Catálogo de Endpoints

URL Base: `https://api.facil-factura.pro` (Sujeto a ambiente QA/PROD)

| Documento | Endpoint | Método |
| :--- | :--- | :--- |
| **Factura de Venta** | `/api/invoices` | POST |
| **Nota Crédito** | `/api/credit-notes` | POST |
| **Nota Débito** | `/api/debit-notes` | POST |
| **Nómina Electrónica** | `/api/payroll` | POST |
| **Documento Soporte** | `/api/support-documents` | POST |
| **Doc. Equivalente (POS)** | `/api/equivalent-documents/pos` | POST |
| **Sector Salud (RIPS)** | `/api/health-invoices/rips` | POST |
| **Sector Transporte** | `/api/transport-invoices` | POST |
| **Eventos (Acuses/Radian)**| `/api/reception-events` | POST |

---

## 4. Estructuras JSON Esperadas (Payloads)

A continuación, se presentan los esqueletos simplificados de los JSON que espera cada endpoint. 
*(Nota: El catálogo completo de catálogos paramétricos DIAN -ciudades, impuestos, unidades de medida- se encuentra en el Anexo Técnico V1.9).*

### 4.1 Factura Electrónica Estándar (`/api/invoices`)
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

### 4.2 Nota Crédito (`/api/credit-notes`)
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

### 4.3 Documento Equivalente POS (`/api/equivalent-documents/pos`)
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

### 4.4 RIPS Sector Salud (`/api/health-invoices/rips`)
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

### 4.5 Sector Transporte de Carga (RNDC) (`/api/transport-invoices`)
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

**Al Consultar Estado (GET /api/documents/{TrackingId}/status):**
```json
{
  "trackId": "POS-10045",
  "status": "ACCEPTED",
  "dianResponse": "Procesado Correctamente",
  "cufe": "3a4b5c6d...",
  "filesUrl": "/api/documents/POS-10045/files"
}
```
