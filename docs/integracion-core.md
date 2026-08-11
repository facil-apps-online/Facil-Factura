# Facil Factura — Integración con el ecosistema FacilApps (Core)

> Estado: **Diseño validado**
> Fecha: 2026-08-03
> Alcance: integración de Facil Factura con Core para planes/productos por país, creación de tenants, consumos y pagos.

---

## 1. Principio rector

**El auth queda a nivel del proyecto** (como en Glamtica/Nexu/Core): Facil Factura conserva su autenticación local
(SQL Server, BCrypt) y su token `x-tenant-id`. **No** se sustituye por el JWT de Core.

Core es la fuente de verdad de **comercial** (planes, productos, suscripciones, cobros, pagos).
Facil Factura es la fuente de verdad de **operación** (clientes, resoluciones, certificados, documentos, RIPS).

Lo que sí se controla desde Core:

| Necesidad | Flujo |
|---|---|
| Cargar consumos del tenant a Core | Diario / fin de mes, para emitir las facturas del tenant |
| Planes y productos disponibles por país | Facil Factura consulta planes/limitaciones de Core (multipaís) |
| Creación de tenants | Se crea desde Facil Factura, pero **debe quedar en Core** |
| Registros de pagos | Pasan de vuelta a Facil Factura para que el cliente descargue las facturas de sus pagos |

---

## 2. Identidades y llaves

### 2.1 Plataforma en Core

- `platforms.id` de Facil Factura: **`acd97b41-2e4d-4742-9a80-5e6e9acb7958`** (referenciado en `platform_reporting_config`, pendiente de creación real en `platforms`).
- API key reporting de la plataforma: `faculfactura_live_m4n6b8v0c2x5z1w3` (prefix `faculfactura_live_`).
- **Pendiente**: crear el registro `platforms` de Facil Factura (Nombre, base_url, países operativos).

### 2.2 Comunicación servidor-a-servidor

Facil Factura (servidor) llama a Core autenticándose con la **service role key** de Core (clave con bypass de RLS).
Core se comunica con Facil Factura (pagos → webhook) usando un **Service Secret** (`FAO_..._SERVICE_SECRET`) propio,
firmando/via HTTP una cabecera de confianza.

> No exponer nunca la service role key en los frontends; solo desde las APIs .NET / worker.

---

## 3. Modelo de datos en Core (ya existente)

Tablas Core reutilizadas:

| Tabla | Uso |
|---|---|
| `platforms` | Registro de la plataforma y pa países operativos (`platform_countries`) |
| `subscription_plans` | Planes de la plataforma, `billing_frequency_months`, `is_default_trial` |
| `plan_assets` | Productos/recursos de la plataforma (docs, clientes, RIPS, usuarios…) |
| `plan_country_configurations` | Plan activo por país (multipaís) con `features[]` |
| `plan_asset_limits` | Límite/valor + `extra_unit_price` / `overage_unit_price` por asset |
| `tenants` | Registro comercial del tenant creado desde Facil Factura |
| `tenant_subscriptions` | Suscripción activa del tenant (plan, fechas, trial) |
| `transactions` | Pagos confirmados (reemplazó `payment_intents`/`payments`) |
| `monthly_charges` | Cargo mensual del tenant (base + overage) |
| `platform_assignments` | Vinculación usuario↔platform (uso opcional si es multiplataforma) |

---

## 4. Planes y servicios por país (multipaís)

**Decisión (2026-08-03):** Facil Factura maneja su **propio modelo de precios** (TariffTier local en
FelDb, pago por documento por niveles). Core **NO** almacena los precios de Facil Factura.

El **único** uso de Core en este punto es: registrar en `platform_countries` qué países tiene
habilitado el producto, para **describir los servicios disponibles por país** (catálogo de
documentos/tipos según país). Actualmente solo **Colombia (CO)** está habilitado (código UBL
hardcodeado a CO; la API ya recibe `{country}` como parámetro de ruta). Se aspira a ampliar el
portafolio a otros países (CL/AR/ES) en el futuro.

**Estado en Core (verificado 2026-08-03):**
- `platforms` Facil Factura: `acd97b41-2e4d-4742-9a80-5e6e9acb7958` / slug `facil-factura` /
  status `development` / base_url `https://facil-factura.pro`.
- `platform_countries`: ✓ **Colombia (CO)** asociada. Vacío: planes, tenants.
- Países disponibles en Core: AR, CL, CO, ES (Glamtica tiene los 4 asociados).

La descripción de "servicios disponibles por país" puede exponerse desde Facil Factura consultando
Core los países de la plataforma (`platform_countries`) y cruzando con el catálogo local de
`DocumentTypes`.

---

## 5. Consumos del tenant → Core (carga diaria / fin de mes)

**Dirección:** Facil Factura → Core.

- FelDb ya cuenta:
  - `TenantBilling` (Month, Year, TotalDocuments, TotalAmount, Status).
  - `TariffTier` por niveles (Nivel 1…5 con `PricePerDocument`).
- Se debe exponer en Core el acumulado de documentos emitidos por tenant para facturar a fin de mes.

**Propuesta:**
1. Worker de Facil Factura (nico técnico, adelantado a fin de mes) envía a Core el total de
   documentos emitidos por `tenant` en el periodo (`billing_period_start/end`).
2. Core lo refleja en `monthly_charges`: `base_plan_charge` + `total_overage_charge` = `total_charge`.
3. Facil Factura guarda/aconseja el `reference` y estado del mesh.

**Endpoint propuesto en Core (edge function `fel-sync-usage` o vía RPC):**
```
POST /functions/fel-sync-usage
X-Service-Secret: …
{ "tenant_core_id": uuid,
  "period_start": "2026-08-01", "period_end": "2026-08-31",
  "documents": 1234 }
```
→ devuelve `{ monthly_charge_id, total_charge, status }`.

---

## 6. Pagos (Core → Facil Factura)

**Flujo:** 1. tenant paga en Core (wompi). 2. Core registra `transactions` + `tenant_subscriptions` activa.
3. Core debe **notificar a Facil Factura** para que el cliente pueda descargar la factura de su pago.

**Dos opciones (recomendada A):**

- **A (webhook/edge + consulta):** Core, al resolver una transacción de una plataforma destino
  (Facil Factura), hace `POST` a `/api/fel/webhooks/payment` de Facil Factura con
  `{ transaction_id, tenant_id, reference, amount, currency, fecha }`.
  Facil Factura guarda el registro de pago y emite el PDF de recibo que el cliente descarga.
- **B (polling):** Facil Factura consulta `transactions` de Core por plataforma/tenant cada cierto tiempo.

## 7. LLaves y variables de entorno Facil Factura

En el compose/`appsettings` se añaden:
```
Core__SupabaseUrl=...
Core__ServiceRoleKey=...
Core__PlatformId=acd97741-2e4d-4742-9a80-5e6e9acb7958
```
En Core, para el webhook hacia Facil Factura:
```
FACILFACTURA_WEBHOOK_URL=https://api.facil-factura.pro/api/fel/webhooks/payment
FACILFACTURA_WEBHOOK_SECRET=...
```

---

## 8. Planes de trabajo

- [ ] Registrar `platforms` de Facil Factura en Core (con `platform_countries` = CO).
- [ ] Definir `asset_key` de la plataforma (docs, RIPS) y plan (sender/productos en Core).
- [ ] Crear RPC/`fel-sync-usage` para consumos + documental que refleje `monthly_charges`.
- [ ] Cliente .NET (fref) para consultar planes (registro cod) y crear tenants en Core.
- [ ] Al crear tenant hacia Facil Factura: crear también en Core (`register-tenant` / RPC).
- [ ] Servicio de pagos en Facil Factura + endpoint webhook de pago + descarga de recibo.
- [ ] Worker de carga diaria de consumos a Core.

---

## 9. Decisiones abiertas (avance)

| # | Pregunta | Estado |
|---|---|---|
| 1 | ¿Apagar `auth`/SSO de core para facilitar la caída? (se mantiene local) | NO aplica |
| 2 | ¿Los límites de plan se validan localmente (wor.cache) o contra Core en cada emisión? | Por validar |
| 3 | ¿Tarifas por niveles (TariffTier local) se reemplazan por planes de Core? | **No** — precios quedan locales (decisión 2026-08-03) |
| 4 | ¿Moneda/pais por país configurado solo en Core? | Parcial — país sí (descripción de servicios); precios locales |
| 5 | ¿El tenant crea a sus clientes en Core también? | No (solo tenant) |