// Fuente única de los ejemplos de la "Guía de integración": una factura de muestra y, por operación, el código en cada lenguaje.
// Para cambiar un ejemplo o sumar un lenguaje se edita aquí; la pantalla solo los muestra.

export const API_BASE = 'https://api.facil-factura.pro';

export const LANGUAGES = [
  { key: 'curl', label: 'cURL' },
  { key: 'python', label: 'Python' },
  { key: 'php', label: 'PHP' },
  { key: 'csharp', label: 'C#' },
  { key: 'node', label: 'Node.js' },
  { key: 'java', label: 'Java' },
] as const;
export type LanguageKey = typeof LANGUAGES[number]['key'];

export const SAMPLE_INVOICE = `{
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
  ]
}`;

export interface GuideOperation {
  key: string;
  title: string;
  description: string;
  method: 'GET' | 'POST';
  path: string;
  comingSoon?: boolean;
  samples: Record<LanguageKey, string>;
}

const indent = (text: string, spaces: number) => text.split('\n').map((l, i) => (i === 0 ? l : ' '.repeat(spaces) + l)).join('\n');

const emit: GuideOperation = {
  key: 'emit',
  title: 'Emitir una factura',
  description:
    'Envía la factura con el consecutivo (number) que lleva tu sistema, dentro del rango de la resolución. La respuesta es síncrona: trae el id (UUID), el estado y el CUFE.',
  method: 'POST',
  path: '/api/co/dian/invoices',
  samples: {
    curl: `curl -X POST ${API_BASE}/api/co/dian/invoices \\
  -H "x-api-key: TU_API_KEY" \\
  -H "x-api-secret: TU_API_SECRET" \\
  -H "Content-Type: application/json" \\
  -d '${indent(SAMPLE_INVOICE, 0)}'`,
    python: `import requests

invoice = ${indent(SAMPLE_INVOICE.replace(/"(\w+)": null/g, '"$1": None'), 0)}

r = requests.post(
    "${API_BASE}/api/co/dian/invoices",
    headers={"x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET"},
    json=invoice,
)
print(r.status_code, r.json())  # 201 -> {"id": "...", "status": "...", "cufe": "..."}`,
    php: `<?php
$invoice = '${SAMPLE_INVOICE}';

$ch = curl_init("${API_BASE}/api/co/dian/invoices");
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $invoice,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => [
        "x-api-key: TU_API_KEY",
        "x-api-secret: TU_API_SECRET",
        "Content-Type: application/json",
    ],
]);
$response = curl_exec($ch);
echo curl_getinfo($ch, CURLINFO_HTTP_CODE), " ", $response;`,
    csharp: `using System.Net.Http;
using System.Text;

var invoice = """
${SAMPLE_INVOICE}
""";

using var http = new HttpClient { BaseAddress = new Uri("${API_BASE}") };
http.DefaultRequestHeaders.Add("x-api-key", "TU_API_KEY");
http.DefaultRequestHeaders.Add("x-api-secret", "TU_API_SECRET");

var response = await http.PostAsync("/api/co/dian/invoices", new StringContent(invoice, Encoding.UTF8, "application/json"));
Console.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");`,
    node: `const invoice = ${indent(SAMPLE_INVOICE, 0)};

const res = await fetch("${API_BASE}/api/co/dian/invoices", {
  method: "POST",
  headers: {
    "x-api-key": "TU_API_KEY",
    "x-api-secret": "TU_API_SECRET",
    "Content-Type": "application/json",
  },
  body: JSON.stringify(invoice),
});
console.log(res.status, await res.json());`,
    java: `import java.net.URI;
import java.net.http.*;

String invoice = """
${SAMPLE_INVOICE}
""";

HttpRequest request = HttpRequest.newBuilder(URI.create("${API_BASE}/api/co/dian/invoices"))
    .header("x-api-key", "TU_API_KEY")
    .header("x-api-secret", "TU_API_SECRET")
    .header("Content-Type", "application/json")
    .POST(HttpRequest.BodyPublishers.ofString(invoice))
    .build();

HttpResponse<String> response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
System.out.println(response.statusCode() + " " + response.body());`,
  },
};

const status: GuideOperation = {
  key: 'status',
  title: 'Consultar por UUID',
  description: 'Usa el id que devolvió la emisión. Solo ves los documentos de tu propio emisor; cualquier otro id responde 404.',
  method: 'GET',
  path: '/api/co/dian/documents/{id}',
  samples: {
    curl: `curl ${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO \\
  -H "x-api-key: TU_API_KEY" \\
  -H "x-api-secret: TU_API_SECRET"`,
    python: `import requests

r = requests.get(
    "${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO",
    headers={"x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET"},
)
print(r.status_code, r.json())`,
    php: `<?php
$ch = curl_init("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO");
curl_setopt_array($ch, [
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ["x-api-key: TU_API_KEY", "x-api-secret: TU_API_SECRET"],
]);
echo curl_exec($ch);`,
    csharp: `using var http = new HttpClient { BaseAddress = new Uri("${API_BASE}") };
http.DefaultRequestHeaders.Add("x-api-key", "TU_API_KEY");
http.DefaultRequestHeaders.Add("x-api-secret", "TU_API_SECRET");

Console.WriteLine(await http.GetStringAsync("/api/co/dian/documents/ID_DEL_DOCUMENTO"));`,
    node: `const res = await fetch("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO", {
  headers: { "x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET" },
});
console.log(res.status, await res.json());`,
    java: `HttpRequest request = HttpRequest.newBuilder(URI.create("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO"))
    .header("x-api-key", "TU_API_KEY")
    .header("x-api-secret", "TU_API_SECRET")
    .GET()
    .build();

System.out.println(HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString()).body());`,
  },
};

const pdf: GuideOperation = {
  key: 'pdf',
  title: 'Descargar el PDF',
  description:
    'Descarga directa del PDF del documento (application/pdf), con la plantilla que el emisor eligió en el portal. También llega en Base64 dentro de /files y con ?include=files al emitir.',
  method: 'GET',
  path: '/api/co/dian/documents/{id}/pdf',
  samples: {
    curl: `curl -o factura.pdf ${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf \\
  -H "x-api-key: TU_API_KEY" \\
  -H "x-api-secret: TU_API_SECRET"`,
    python: `import requests

r = requests.get(
    "${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf",
    headers={"x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET"},
)
open("factura.pdf", "wb").write(r.content)`,
    php: `<?php
$ch = curl_init("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf");
curl_setopt_array($ch, [
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ["x-api-key: TU_API_KEY", "x-api-secret: TU_API_SECRET"],
]);
file_put_contents("factura.pdf", curl_exec($ch));`,
    csharp: `using var http = new HttpClient { BaseAddress = new Uri("${API_BASE}") };
http.DefaultRequestHeaders.Add("x-api-key", "TU_API_KEY");
http.DefaultRequestHeaders.Add("x-api-secret", "TU_API_SECRET");

await File.WriteAllBytesAsync("factura.pdf", await http.GetByteArrayAsync("/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf"));`,
    node: `import { writeFile } from "node:fs/promises";

const res = await fetch("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf", {
  headers: { "x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET" },
});
await writeFile("factura.pdf", Buffer.from(await res.arrayBuffer()));`,
    java: `HttpRequest request = HttpRequest.newBuilder(URI.create("${API_BASE}/api/co/dian/documents/ID_DEL_DOCUMENTO/pdf"))
    .header("x-api-key", "TU_API_KEY")
    .header("x-api-secret", "TU_API_SECRET")
    .GET()
    .build();

HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofFile(java.nio.file.Path.of("factura.pdf")));`,
  },
};

// Factura del sector salud: la factura de siempre más el bloque health y, opcional, el RIPS soporte (abreviado aquí; cada servicio lleva todos los campos de la
// Resolución 2275 de 2023). Solo con emisión directa a la DIAN.
export const SAMPLE_HEALTH_INVOICE = `{
  "number": "1001",
  "issue_date": "01/11/2026",
  "payment_means_type": "DEBITO",
  "payment_means": "CASH",
  "numbering": { "prefix": "SETT", "resolution_number": "18760000001" },
  "customer": {
    "party_identification_type": "NIT",
    "party_identification": "800111222",
    "company_name": "EPS EJEMPLO SA"
  },
  "items": [
    { "sku": "CONS01", "description": "Consulta médica general", "quantity": 1, "price": 38000 }
  ],
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
}`;

const HEALTH_PATH = '/api/co/dian/health-invoices';

const health: GuideOperation = {
  key: 'health',
  title: 'Emitir una factura del sector salud',
  description:
    'Factura de un prestador a una entidad responsable de pago, con el bloque health (prestador, modalidad de pago, cobertura, contrato y período) y el RIPS soporte. Solo con emisión directa a la DIAN. Si el RIPS no cuadra con la factura se responde 422 RIPS_INVALID y no se emite nada. Con la factura aprobada, el RIPS se envía al Ministerio de Salud y la respuesta trae en rips el estado y el CUV (ACCEPTED), las glosas (REJECTED) o PENDING si no hubo respuesta; en ese caso se reenvía con POST /api/co/dian/health-invoices/{id}/rips. Puedes probar el cuerpo sin emitir en /api/co/dian/health-invoices/validate.',
  method: 'POST',
  path: HEALTH_PATH,
  samples: {
    curl: `curl -X POST ${API_BASE}${HEALTH_PATH} \\
  -H "x-api-key: TU_API_KEY" \\
  -H "x-api-secret: TU_API_SECRET" \\
  -H "Content-Type: application/json" \\
  -d '${SAMPLE_HEALTH_INVOICE}'`,
    python: `import requests

invoice = ${SAMPLE_HEALTH_INVOICE.replace(/"(\w+)": null/g, '"$1": None')}

r = requests.post(
    "${API_BASE}${HEALTH_PATH}",
    headers={"x-api-key": "TU_API_KEY", "x-api-secret": "TU_API_SECRET"},
    json=invoice,
)
print(r.status_code, r.json())  # 201 -> {"id": "...", "cufe": "...", "rips": {"status": "ACCEPTED", "cuv": "..."}}`,
    php: `<?php
$invoice = '${SAMPLE_HEALTH_INVOICE}';

$ch = curl_init("${API_BASE}${HEALTH_PATH}");
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $invoice,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => [
        "x-api-key: TU_API_KEY",
        "x-api-secret: TU_API_SECRET",
        "Content-Type: application/json",
    ],
]);
$response = curl_exec($ch);
echo curl_getinfo($ch, CURLINFO_HTTP_CODE), " ", $response;`,
    csharp: `using System.Net.Http;
using System.Text;

var invoice = """
${SAMPLE_HEALTH_INVOICE}
""";

using var http = new HttpClient { BaseAddress = new Uri("${API_BASE}") };
http.DefaultRequestHeaders.Add("x-api-key", "TU_API_KEY");
http.DefaultRequestHeaders.Add("x-api-secret", "TU_API_SECRET");

var response = await http.PostAsync("${HEALTH_PATH}", new StringContent(invoice, Encoding.UTF8, "application/json"));
Console.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");`,
    node: `const invoice = ${SAMPLE_HEALTH_INVOICE};

const res = await fetch("${API_BASE}${HEALTH_PATH}", {
  method: "POST",
  headers: {
    "x-api-key": "TU_API_KEY",
    "x-api-secret": "TU_API_SECRET",
    "Content-Type": "application/json",
  },
  body: JSON.stringify(invoice),
});
console.log(res.status, await res.json());`,
    java: `import java.net.URI;
import java.net.http.*;

String invoice = """
${SAMPLE_HEALTH_INVOICE}
""";

HttpRequest request = HttpRequest.newBuilder(URI.create("${API_BASE}${HEALTH_PATH}"))
    .header("x-api-key", "TU_API_KEY")
    .header("x-api-secret", "TU_API_SECRET")
    .header("Content-Type", "application/json")
    .POST(HttpRequest.BodyPublishers.ofString(invoice))
    .build();

HttpResponse<String> response = HttpClient.newHttpClient().send(request, HttpResponse.BodyHandlers.ofString());
System.out.println(response.statusCode() + " " + response.body());`,
  },
};

export const OPERATIONS: GuideOperation[] = [emit, health, status, pdf];

export const ERROR_CODES: { code: string; http: number; meaning: string }[] = [
  { code: 'MISSING_CREDENTIALS / INVALID_API_KEY / INVALID_API_SECRET', http: 401, meaning: 'Falta un header o la llave o el secreto no son válidos.' },
  { code: 'VALIDATION_ERROR', http: 400, meaning: 'Datos inválidos; details lista cada campo con el motivo.' },
  { code: 'RESOLUTION_NOT_FOUND / NUMBER_OUT_OF_RANGE / RESOLUTION_EXPIRED', http: 422, meaning: 'El prefijo, el número o la vigencia no corresponden a una resolución habilitada. El mensaje lista las disponibles.' },
  { code: 'DOCUMENT_ALREADY_EXISTS', http: 409, meaning: 'Ese número ya fue usado; la respuesta trae el id y el estado del documento existente.' },
  { code: 'PROVIDER_REJECTED', http: 422, meaning: 'La DIAN o el integrador rechazaron la factura. Corrige y reenvía el mismo número.' },
  { code: 'PROVIDER_UNAVAILABLE', http: 502, meaning: 'No se pudo confirmar la emisión. No reenvíes: consulta el estado con el id.' },
  { code: 'DOCUMENT_NOT_FOUND', http: 404, meaning: 'El id no existe o no pertenece a tu emisor.' },
  { code: 'HEALTH_NATIVE_ONLY', http: 422, meaning: 'La factura del sector salud solo se emite con emisión directa a la DIAN.' },
  { code: 'RIPS_INVALID', http: 422, meaning: 'El RIPS no cuadra con la factura; details lista cada regla. No se emitió nada.' },
  { code: 'RIPS_REJECTED', http: 422, meaning: 'El Ministerio de Salud rechazó el RIPS; rips.findings trae las glosas. La factura ya está emitida: corrige y reenvía el RIPS.' },
];
