# Matriz de documentos: qué emite el sistema y por dónde

> Estado: **inventario al 2026-11-01** (levantado leyendo el código; no se probó cada flujo contra la DIAN).
> Sirve como guía de referencia para la simplificación de la API de integración.

Leyenda: ✅ funciona · ⚠️ por verificar · ❌ no existe · — no aplica

- **Portal nativo / Portal Dataico**: se puede emitir desde el portal del cliente (`api/client/*`) según el integrador del emisor.
- **API hoy**: endpoint en `api/co/dian/*` (API de integración), tal como está antes del rediseño.
- **Ver en portal**: el documento emitido por API aparece en la lista del portal (objetivo del rediseño: todos).

## 1. Facturación

| Documento | Código catálogo | DIAN | Portal nativo | Portal Dataico | API hoy | Notas |
|---|---|---|---|---|---|---|
| Factura estándar | FE-STD | 01 / op 10 | ✅ | ✅ | ✅ 202, solo NATIVE | |
| Factura de mandato | FE-MANDATO | 01 / op 11 | ✅ | ✅ | ⚠️ (comparte `/invoices`) | Cada línea debe traer mandante |
| Factura AIU | FE-AIU | 01 / op 09 | ✅ | ✅ | ⚠️ | Exige objeto del contrato y líneas A, I, U |
| Factura de exportación | FE-EXP | 02 / op 10 | ✅ | ✅ | ⚠️ | |
| Factura de transporte | FE-TRANSP | 01 / op 15 | ❌ (no está en `SupportedDianCodes`) | ❌ (solo directo) | ✅ 202, solo NATIVE | |
| Factura sector salud (RIPS) | FE-SALUD | 01 / op 10 | ❌ | ❌ | ❌ 501 | |
| Contingencia facturador / DIAN | FC-FACT, FC-DIAN | 03 / 04 | ❌ | ❌ | ❌ sin endpoint | Definir si se expone |
| Nota crédito | NC | 91 | ✅ (con y sin factura) | ✅ (con y sin factura) | ✅ síncrono 201, Dataico y NATIVE; con y sin factura | Sin factura: `customer` + `items` + `reason` |
| Nota débito | ND | 92 | ✅ (solo con factura) | ✅ (solo con factura) | ✅ síncrono 201, Dataico y NATIVE; solo con factura | Sin factura: pendiente (motor nativo y confirmar Dataico) |

## 2. Documento soporte y nómina

| Documento | Código catálogo | DIAN | Portal nativo | Portal Dataico | API hoy | Notas |
|---|---|---|---|---|---|---|
| Documento soporte | DS | 05 | ✅ | ✅ | ✅ síncrono 201, Dataico y NATIVE | Mismo motor (`SupportDocumentPublishingService`) para portal y API; vendedor en `customer`, sin impuestos en líneas |
| Nota de ajuste de doc. soporte | DS-AJUSTE | 95 | ✅ | ✅ | ✅ síncrono 201, Dataico y NATIVE | `support-documents/adjustment`; sin `items` replica las líneas del documento |
| Nómina electrónica | NE-PAGO | 102 | ❌ (el portal exige Dataico) | ✅ | ✅ síncrono, solo NATIVE | XML nativo ya toma conceptos del catálogo |
| Nota de ajuste de nómina (reemplazo / eliminación) | NE-AJUSTE | 103 | ❌ | ✅ | ✅ `payroll` y `payroll/void` | |

## 3. Documentos equivalentes

Hoy solo existen en la API de integración (nativo, síncronos). El portal no los emite (`SupportedDianCodes` = 01, 02, 91, 92).

| Documento | Código catálogo | DIAN | Portal | API hoy | Notas |
|---|---|---|---|---|---|
| Tiquete POS | DE-POS | 20 | ❌ | ✅ `/equivalent-documents/pos` | |
| Cine | DE-CINE | 25 | ❌ | ✅ | |
| Espectáculos públicos | DE-ESPECTACULOS | 27 | ❌ | ✅ | |
| Juegos localizados / suerte y azar | DE-JUEGOSLOC, DE-AZAR | 30 | ❌ | ✅ | |
| Transporte terrestre de pasajeros | DE-PASAJEROS | 35 | ❌ | ✅ | |
| Peajes | DE-PEAJE | 40 | ❌ | ✅ | |
| Extracto | DE-EXTRACTO | 45 | ❌ | ⚠️ | `ProfileID` por confirmar |
| Transporte aéreo | DE-AEREO | 50 | ❌ | ✅ | |
| Bolsa de valores / agropecuaria | DE-BOLSA, DE-AGRO | 55 | ❌ | ✅ | |
| Servicios públicos | DE-SERVICIOSP | 60 | ❌ | ⚠️ | `ProfileID` por confirmar |
| Nota de ajuste equivalente (crédito / débito) | DE-AJUSTE-CREDITO / -DEBITO | 94 / 93 | ❌ | ❌ sin endpoint | |

Dataico no maneja documentos equivalentes: solo nativo.

## 4. Eventos RADIAN (documentos recibidos)

| Función | Portal nativo | Portal Dataico | API hoy |
|---|---|---|---|
| Acuse, recibo, aceptación expresa, reclamo | ✅ (firma con certificado propio) | ✅ (firma Dataico) | ✅ `received-documents` (ingreso, lista, eventos 030–033, historial) |
| Aceptación tácita (034) | ❌ | ❌ | ❌ |
| Archivo del evento | ✅ XML firmado (guardado desde ahora) | ✅ XML (sin PDF) | ✅ `events/{eventId}/file` |

## 5. Consulta y entrega de archivos (API hoy)

| Función | Estado |
|---|---|
| Estado por `TrackingId` (prefijo + número) | ✅ pero no por UUID |
| XML / PDF en Base64 (`/files`) | ⚠️ devuelve la URL guardada o `"PdfNoDisponible"`, no Base64 real |
| Resoluciones por API (listar / sincronizar) | ❌ solo en el portal |
| Consulta de adquirente | ✅ solo NATIVE |
| Documentos emitidos por API visibles en el portal | ⚠️ solo los que pasan por el worker (cola); los síncronos por verificar |

## 6. Resumen de brechas que cierra el rediseño

1. Todos los endpoints de factura aceptan el integrador del emisor (nativo o Dataico), no solo NATIVE.
2. Respuesta uniforme `202 + UUID` y consulta por UUID.
3. Todo documento emitido por API queda como `Document` y se ve en el portal.
4. Nómina y documento soporte: el portal solo emite por Dataico; el nativo vive solo en la API. Hay que unificar.
5. Equivalentes: solo nativo; definir si el portal los incorpora.
6. `files` con Base64 real; `/pdf` autenticado y ligado al emisor.
7. Resoluciones por API (hecho); eventos RADIAN por API (hecho).
