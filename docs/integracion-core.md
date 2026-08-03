# Facil Factura — Integración con el ecosistema FacilApps (Core)

> Estado: **Borrador para validación**
> Fecha: 2026-08-02
> Alcance: diseño de integración de Facil Factura con Core (SSO, tenants, planes, pagos, reporting)

---

## 1. Objetivo

Definir cómo Facil Factura (facturación electrónica FEL, DIAN) se integra con el **Core central** del ecosistema FacilApps, siguiendo el patrón ya probado en Glamtica, Tattoo Suite y NexuHR.

**Principio rector:** doble presencia de tenants.
- **Core** (Supabase central `lvdrwumtbhvbtolqgrwi`): identidad del tenant, cobros, suscripciones, pagos, límites de plan, integraciones.
- **FacilFactura** (SQL Server `FelDb`): reglas de negocio FEL — clientes, resoluciones DIAN, certificados, documentos, RIPS, tarifas.

---

## 2. Arquitectura general

```
┌─────────────────────────────────────────────────────────────────┐
│                        FACIL FACTURA                            │
│                                                                 │
│  apps/tenant-web      apps/client-web      apps/superadmin-web  │
│  (tenants.facil-factura.pro) (clients...) (admin...)            │
│                        │ (JWT de Core)                          │
│  ┌──────────────────────────────────────────┐                   │
│  │  Fel.Api.Tenant / Client / Superadmin    │  (.NET 9 + DIAN)  │
│  │  - API Key HMAC (B2B)                    │                   │
│  │  - JWT Supabase/Core (web)               │                   │
│  └──────────────────┬───────────────────────┘                   │
│                     │                                           │
│  ┌──────────────────┴───────────────────┐                       │
│  │  Fel.Infrastructure (EF Core)        │  Fel.Worker (Redis)   │
│  │  SQL Server: FelDb                   │  cola fel:invoices    │
│  └──────────────────┬───────────────────┘                       │
└─────────────────────┼───────────────────────────────────────────┘
                      │ CORE_SUPABASE_URL + SERVICE_ROLE_KEY
┌─────────────────────┴───────────────────────────────────────────┐
│                       CORE (Supabase central)                    │
│  platforms · tenants · subscription_plans · tenant_subscriptions │
│  payments → transactions · payment_intents · monthly_charges     │
│  platform_assignments · tenant_integrations · wompi webhooks     │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. Identidad y autenticación (híbrido)

### 3.1 Usuarios web (apps React)
- Login contra **Core** (Supabase Auth). El JWT emitido por Core lleva `sub` (user id).
- La relación **usuario → tenant → plataforma** se resuelve vía `platform_assignments` en Core (`user_id`, `platform_id` de Facil Factura, `role_id`).
- Las edge functions de FacilFactura (futuro proyecto Supabase propio) validan el JWT y consultan Core para confirmar que el usuario está asignado a la plataforma y a un tenant activo.

### 3.2 Integración B2B (APIs .NET)
- Se mantiene el esquema actual: **API Key** + firma HMAC (`x-api-key`, `x-api-timestamp`, `x-api-signature`) en `Fel.Api.Integration` (endpoints `/api/invoices`, `/api/credit-notes`, etc.).
- La API key se emite desde FacilFactura (tabla `Clients.LiveApiKey`) y opcionalmente se refleja en `tenant_integrations` de Core para trazabilidad.
- `Fel.Api.Tenant` y `Fel.Api.Client` también exponen su seguridad actual; se añadirá validación de JWT de Core para el acceso web.

### 3.3 Modelo de datos de seguridad en FacilFactura (SQL Server)
| Entidad | Propósito |
|---|---|
| `TenantUser` | usuarios web del tenant (vinculados a `auth.users` de Core por `sub`) |
| `ClientUser` | usuarios del cliente final |
| `SuperadminUser` | operadores de la plataforma |
| `Client.LiveApiKey` | API key B2B (HMAC) |

---

## 4. Tenants (doble presencia)

### 4.1 Core (`tenants`)
Tabla ya existente en Core (`20251227000010_create_definitive_core_schema.sql`):
- `id`, `name`, `subscription_status` (trial/active/canceled), `is_active`
- Datos fiscales: `legal_name`, `tax_id`, `billing_address`, `website`, `whatsapp_phone`, `einvoicing_email`, dirección física

### 4.2 FacilFactura (SQL Server `Fel.Tenant`)
- Modelo propio con reglas de negocio (branding, configuración de documentos, etc.).
- Se añade una **columna de vínculo** `CoreTenantId` (uuid) para mantener la correlación.

### 4.3 Ciclo de vida
1. **Creación**: un superadmin crea el tenant en Core (con plan y cobro). Se propaga a FelDb (automático vía edge function o manual en la API).
2. **Activación/estado**: Core es la fuente de verdad del estado de suscripción. FacilFactura consulta Core (edge function `get-tenant-plan`) para saber si el tenant puede emitir documentos.
3. **Sincronización**: `tenant_subscriptions` (Core) → estado de emisión (FacilFactura). Si el plan está cancelado, FacilFactura bloquea nuevas emisiones.

---

## 5. Planes, suscripciones y pagos (en Core)

Reutilizamos la infraestructura de cobro de Core (wompi), sin duplicar:

| Capa | Tabla (Core) | Uso |
|---|---|---|
| Plan | `subscription_plans` + `plan_assets` | define límites (clientes, docs/mes, RIPS) |
| País/precio | `plan_country_configurations` | precios por país (COL) |
| Suscripción | `tenant_subscriptions` | tenant activo o no |
| Checkout | `payment_intents` | intento de pago wompi |
| Pago | `transactions` (reemplaza `payments`) | transacciones confirmadas |
| Recurrente | `monthly_charges` | cobro mensual programado |

**Flujo de pago (ya operativo en Core):**
```
tenant-web → edge function wompi-generate-checkout (Core)
         → payment_intents (Core) → wompi checkout → webhook wompi (Core)
         → transactions (Core) → tenant_subscriptions.is_active = true
FacilFactura consulta estado de suscripción antes de emitir.
```

---

## 6. Reporting (integración con FacilReports)

- Facil Factura ya está registrado en `platform_reporting_config` (Core) con:
  - `platformId`: `acd97b41-2e4d-4742-9a80-5e6e9acb7958`
  - `apiKeyPrefix`: `faculfactura_live_`
  - API key: `faculfactura_live_m4n6b8v0c2x5z1w3`
- **Pendiente**: poblar `supabaseUrl`, `supabaseServiceKey`, `driveFolderId` una vez exista el proyecto Supabase propio.
- Los `.repx` de plantillas de documentos FEL se gestionarán con el **vault híbrido** (local + Drive) que ya implementa `FacilReports/Services/GoogleDriveService.cs`.
- La generación de PDFs de factura y documentos FEL se delega a `reports.facil-apps.online`.

---

## 7. Proyecto Supabase propio (futuro)

Siguiendo el patrón de `C:\Desarrollos\supabase\Nexu`:

```
supabase/
  config.toml
  functions/
    _shared/supabaseClients.ts     # cliente admin propio + getCoreSupabaseClient()
    tenant-actions/                # switch de acciones del tenant
    superadmin-actions/            # operaciones de plataforma
    client-actions/                # acciones del cliente final
  migrations/
    <timestamp>_initial_schema.sql
```

- **Auth**: conectado a Core para SSO (mismo patrón que Glamtica/Nexu).
- **Edge functions**: validan JWT de Core, consultan `platform_assignments`, y exponen datos FEL cuando sea necesario.
- La **fuente de verdad de negocio** sigue siendo SQL Server (`Fel.Api.*`), el proyecto Supabase propio es auxiliar para acciones serverless/notificaciones que requieran el contexto de Core.

---

## 8. Decisiones abiertas

| # | Pregunta | Estado |
|---|---|---|
| 1 | ¿Quién crea el tenant? Propuesta: Core (con plan) → propagación a FelDb | PENDIENTE VALIDAR |
| 2 | ¿API keys B2B se reflejan en `tenant_integrations` de Core? | PENDIENTE |
| 3 | ¿Proyecto Supabase propio es indispensable o basta con edge functions en Core? | PENDIENTE |
| 4 | ¿Límites de plan se aplican en FelDb vía Core o por configuración local? | PENDIENTE |
| 5 | ¿Google Drive folder id por tenant o global? | PENDIENTE |
