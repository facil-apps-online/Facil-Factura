# Plan: sucursales en el portal del cliente

Estado: **fases 1 a 4 en producción (5 de octubre de 2026); fases 5 y 6 desplegadas; fase 7 (limpieza final y SMTP unificado) implementada y probada, pendiente de desplegar.** Ver la sección 14.

## 1. Objetivo

Un `Client` (un NIT) puede tener varias sucursales. Cada sucursal opera de forma aislada: sus propios documentos,
usuarios, llaves de API, dirección y cobro. Puede usar sus propias resoluciones o compartir las del cliente. Productos,
terceros y plantillas son compartidos por todo el cliente.

## 2. Decisiones acordadas

| Tema | Decisión |
|---|---|
| Modelo | Entidad nueva `Branch` bajo `Client` (no un `Client` hijo). |
| Existentes | Cada `Client` recibe una sucursal **Principal**; todo lo que ya existe queda en ella. |
| Productos y terceros | Compartidos por cliente. No llevan `BranchId`. |
| Plantillas | Del cliente, pero se llenan con los datos de la sucursal (dirección, teléfono, correo). |
| Dirección del documento | La de la sucursal que emite. |
| API de integración | Llaves distintas por sucursal. |
| Resoluciones | Del cliente; pueden usarse en una o varias sucursales. El consecutivo es uno solo por resolución. |
| Prefijos | Únicos por cliente, no por sucursal (la cuenta de Dataico y el software DIAN son del NIT). |
| Nómina, RIPS, recibidos | Por sucursal. |
| Recepción de correo (RADIAN) | Por defecto llega a la principal; cada sucursal puede tener su propio buzón. |
| Creación de sucursales | La crea y activa el tenant directamente. El cliente no crea sucursales. |
| Usuarios | Los crea y gestiona tanto el tenant (desde tenant-web) como el administrador del cliente (desde el portal). Hoy hay un solo usuario por cliente y solo el tenant lo gestiona: el multiusuario es nuevo. |
| Roles del portal | Rol nuevo **Facturador**: emite documentos pero no configura nada. El **Administrador** configura y emite. Los usuarios actuales pasan a Administrador con acceso a todas las sucursales. |
| Cobro del tenant al cliente | Tarifa por sucursal (clientes Dataico). Los demás, por plan. |
| Planes y bolsas prepago | Del cliente, compartidos por todas las sucursales. Plan exclusivo por sucursal: fase posterior. |
| Cobro del superadmin al tenant (`PerUser`) | Cuenta **sucursales activas** en vez de clientes activos. |

## 3. Modelo de datos

### Tabla nueva `Branch`
- Identidad: `Id`, `ClientId`, `Name`, `Code`, `IsMain`, `IsActive`, `CreatedAt` (desde aquí se cobra).
- Ubicación del emisor: dirección, ciudad, código de ciudad, departamento, teléfono, correo.
- Llaves de API: `LiveApiKey/Secret`, `TestApiKey/Secret` (salen de `Client`).
- Tarifa: `SubscriptionRate`, `PricePerDocument` (salen de `Client`).
- Recepción de correo: host, puerto, SSL, usuario, contraseña cifrada, habilitado, y los `AutoSend*` (salen de `Client`).
- Credenciales de MinSalud y de historia clínica (IHCE): por sucursal (ver pendientes).

### Tablas nuevas
- `ResolutionBranch (ResolutionId, BranchId)`: en qué sucursales se puede usar cada resolución.
- `ClientUserBranch (ClientUserId, BranchId)` y en `ClientUser`: `Role` (`Administrador` | `Facturador`) y un indicador
  de "ve todas las sucursales". Un usuario puede pertenecer a varias sucursales.
- `NoteNumbering (ClientId, BranchId?, Tipo, Prefijo, Siguiente)`: contadores de notas crédito, débito y ajuste. `BranchId`
  nulo = contador compartido del cliente (como hoy). Una sucursal con numeración propia tiene su fila con su prefijo.

### Columnas nuevas
- `BranchId` en `Document` (incluye nómina y documento soporte) y en `ReceivedDocument`.
- `BranchId` en `ClientIntegratorBilling` (la tarifa por integrador pasa a ser por sucursal).

### Lo que NO cambia
- Planes y bolsas (`ClientPrepaidPackage`, `ClientPrepaidBag`): siguen por cliente.
- Software DIAN, certificados, credenciales de Dataico, logo, colores, leyendas, SMTP de envío: siguen en `Client`.
- Índice único de `Resolution` `(ClientId, DocumentType, Prefix)`.

## 4. Migración

> **Ajuste (fase 1):** los campos que pasan de `Client` a `Branch` (llaves de API, dirección, tarifa, buzón de correo,
> credenciales de MinSalud/IHCE) **no se copian al crear la sucursal**: quedarían duplicados y se desactualizarían (por
> ejemplo, el tenant rota llaves y edita direcciones sobre `Client`). Cada grupo se mueve junto con la fase que cambia a
> sus lectores, en un solo despliegue, y se elimina de `Client` al terminar: llaves en la fase 2, dirección y numeración de
> notas en la fase 3, tarifa en la fase 5, buzón y credenciales en la fase 6.

1. Crear una sucursal `IsMain` por cada cliente. `Branch.CreatedAt` = `Client.CreatedAt` y `IsActive` = el del cliente,
   para que el cobro actual no cambie.
2. Asignar `BranchId` a documentos y recibidos existentes; relacionar todas las resoluciones y usuarios con la principal.
3. Fase 3: mover los contadores de notas de `Client` a filas `NoteNumbering` sin sucursal.
4. Fase 2: las llaves HMAC existentes pasan a `Branch` con el mismo valor y siguen funcionando.
5. Las columnas viejas de `Client` se eliminan solo después de desplegar el código que ya lee de `Branch`
   (dos pasos, para no dejar datos duplicados ni código muerto).

## 5. Backend

- Un servicio único que resuelve cliente y sucursal de la sesión. Reemplaza los 14 `GetCurrentClientId()` duplicados.
- Header `x-branch-id`, validado contra las sucursales del usuario y contra el token. Sin el header se usa la única
  sucursal del usuario, o se exige elegirla si tiene varias.
- Middleware HMAC: busca la llave en `Branch` y deja `ClientId` y `BranchId` en el contexto. Actualizar la lista de endpoints permitidos.
- Filtros por sucursal en documentos, dashboard, recibidos y nómina. Productos y terceros se siguen filtrando por cliente.
- Al emitir se valida que la resolución esté relacionada con la sucursal activa.
- Roles: el token lleva el rol del usuario y el backend lo exige por endpoint, no solo el portal con menús ocultos.
  - **Facturador:** emitir y consultar documentos de sus sucursales (facturas, soporte, notas, nómina).
  - **Solo Administrador:** resoluciones, plantillas, logo y colores, SMTP, recepción de correo, configuración DIAN,
    numeración de notas, usuarios y sucursales.
  - Productos y terceros: por confirmar si el facturador puede crearlos y editarlos (ver pendientes).
- Usuarios múltiples por cliente: hoy el código asume uno (`FirstOrDefaultAsync(u => u.ClientId == id)` en el tenant y
  la invitación/revocación por cliente). Pasa a operar por usuario, con invitar y también desactivar/revocar.
- El correo de login seguirá siendo único por tenant.
- El cobro de una sucursal inactiva se detiene; la principal no se puede desactivar.

## 6. Emisión

- La dirección del emisor sale de la sucursal en: mapper de Dataico, XML de DIAN nativa y mappers de PDF.
  **Los dos caminos (Dataico y DIAN nativa) se ajustan y se prueban juntos.**
- Revisar contra el anexo técnico qué campos del emisor admite la DIAN para un establecimiento distinto al del RUT,
  antes de implementar en el XML nativo.
- El prefijo ya queda guardado en cada documento (`Document.Prefix`); no cambia.
- Notas: el contador se toma de `NoteNumbering` (el de la sucursal si lo tiene, si no el del cliente).

## 7. Portal del cliente

- Selector de sucursal en el header. Fijo si el usuario solo tiene una.
- Pantalla nueva "Usuarios" (solo Administrador): crear, invitar, asignar rol y sucursales, desactivar.
- El Facturador solo ve en el menú lo que puede usar; el backend lo hace cumplir igual.
- Ajustes por sucursal: recepción de correo, resoluciones disponibles, numeración de notas.
- Dashboard y listados filtrados por la sucursal activa; administradores pueden ver "todas".

## 8. Tenant-web y cobro

- Pestaña "Sucursales" en la edición del cliente: crear, activar, desactivar, tarifa, llaves de API y buzón de correo.
- Pestaña "Usuarios" del cliente: lista múltiple (hoy es un único usuario) con rol y sucursales, invitar y revocar.
- `BillingMetricsService`:
  - Modo `PerUser`: cuenta sucursales activas, prorrateadas por días desde `Branch.CreatedAt`.
  - Tarifa del cliente: `SubscriptionRate` y `PricePerDocument` se leen de la sucursal; los documentos se cuentan por `BranchId`.
  - Bolsas y planes: se descuentan por cliente, con todos los documentos de todas sus sucursales.
- Verificación obligatoria: los montos del último mes de cada cliente existente deben dar **igual** antes y después.

## 9. Recepción, RADIAN, RIPS y nómina

- **Recibidos:** el worker de recepción recorre las sucursales con buzón habilitado. `ReceivedDocument.BranchId` = la
  sucursal cuyo buzón lo recibió. Sin buzón propio, todo sigue llegando a la principal.
- **Eventos RADIAN:** se firman con el certificado del cliente; la sucursal solo determina de cuál buzón salieron y
  los `AutoSend*` que aplican.
- **RIPS / MinSalud / IHCE:** las credenciales pasan a la sucursal. Revisar cómo `fevrips-api` identifica hoy al cliente.
- **Nómina:** son `Document`; queda cubierta con `Document.BranchId`.

## 10. Fases (cada una desplegable por separado)

Orden de despliegue con migraciones: `fel-migrator`, luego APIs, luego el resto.

1. **Modelo y migración** (principal por cliente, sin cambio visible).
2. **Resolución de sucursal en backend** y filtros, HMAC por sucursal, roles exigidos por endpoint.
3. **Emisión:** dirección, resoluciones por sucursal, `NoteNumbering`.
4. **Portal:** selector, pantalla de usuarios y rol Facturador, ajustes por sucursal.
5. **Tenant-web y cobro:** sucursales, usuarios múltiples y cobro por sucursal activa.
6. **Recibidos, RIPS/IHCE y nómina** por sucursal.
7. **Limpieza:** eliminar columnas viejas de `Client`.
8. *(posterior)* Planes y bolsas exclusivos por sucursal (`BranchId` opcional en la bolsa).

## 11. Riesgos y pruebas

- Olvidar un filtro por `BranchId` mezclaría datos entre sucursales del mismo cliente. Se mitiga con el servicio único
  y pruebas de aislamiento (usuario de una sucursal no ve ni emite en otra).
- Prefijos duplicados entre sucursales: validar al crear resoluciones y numeraciones de notas.
- Pruebas: Playwright en el portal; emisión de cada tipo de documento por Dataico y por DIAN nativa; HMAC con llave de
  sucursal; comparación de cobros antes y después de la migración.

## 12. Confirmado por el usuario

- Credenciales de MinSalud e IHCE por sucursal, igual que las llaves de API.
- Usuarios gestionados por el tenant y por el administrador del cliente; rol nuevo Facturador.
- Tarifa por sucursal para clientes con integrador Dataico; los demás, planes compartidos.
- Contadores de notas compartidos por defecto, propios solo si la sucursal lo necesita.
- Planes exclusivos por sucursal: no hay planes de hacer nada especial por sucursal por ahora (se descarta la fase posterior).

- Facturador: puede crear y editar productos y terceros (los necesita para emitir y no son configuración).
- Facturador: no ve los documentos recibidos ni los eventos RADIAN (solo Administrador).
- Facturador: ve el dashboard, pero solo de su sucursal.

## 13. Pendientes

- **RIPS/MinSalud: capítulo aparte.** Hay que rehacer todo el flujo de validación; incluye verificar el envío real con la llave de una sucursal (por ahora la validación de la solicitud corta antes del MUV).
- **Módulo de pagos:** el interruptor por cliente está hecho; falta el módulo en sí (qué controla el sistema de los pagos), que es un capítulo aparte. Hoy la pantalla "Pagos" es solo un texto de "próximamente".
- Facturación automática al cliente: hoy solo se calcula el valor.

## 14. Estado

- **Interruptor de control de pagos por cliente:** implementado y probado; **pendiente de desplegar** (migración `AddClientControlsPayments`, una columna; fel-migrator, fel-api-tenant, fel-api-client, tenant-web y client-web, de uno en uno). `Client.ControlsPayments`, apagado por defecto (clientes existentes y nuevos); lo marca el tenant por cliente en Info Básica, sección "Módulos del portal". `GET /client/session` informa `features.payments`; el portal oculta el menú "Pagos" y la ruta devuelve al inicio cuando está apagado. Un PUT del cliente que no envía el campo no lo apaga. Pruebas: 12 de API, 5 de interfaz de tenant-web, 3 e2e nuevas y 235 e2e en total; regresión de las baterías de las fases 5 y 7.
- **Limpieza del servidor (6 de octubre de 2026):** 24 rutas huérfanas (13 archivos de código sin referencias —9 eliminados del repositorio a propósito y 4 que nunca estuvieron en git—, el directorio `python-signer`, un Dockerfile y 9 logs de despliegue) se movieron a `/opt/orphans-2026-10-06`, fuera del árbol de build. Se dejaron `.env`, sus respaldos, `certificates/fevrips` y `Certificado`, que son del servidor. Los dos avisos de React por inputs con valor nulo ya no se reproducen en ninguna pestaña del cliente.
- **Fase 7 (limpieza final y SMTP unificado):** implementada y probada; **pendiente de desplegar** (migración `RemoveClientBranchColumns`; un servicio a la vez: fel-migrator, fel-api-tenant, fel-api-client, fel-api-superadmin, fel-worker, tenant-web, client-web; entre la migración y el último servicio los servicios viejos fallan al leer columnas que ya no existen).
  - Migración: copia (idempotente) las credenciales de MinSalud, IHCE, buzón y eventos automáticos del Client a su sucursal principal, deja el mayor contador de notas, pasa a la principal lo que no tenía sucursal, vuelve `BranchId` obligatorio en `Documents` y `ReceivedDocuments` y elimina 34 columnas del Client (llaves de API, tarifa, contadores de notas, MinSalud, IHCE, buzón y eventos) con sus 2 índices únicos. El `Down` recrea las columnas y copia los datos de vuelta desde la principal (probado con ida, vuelta e ida).
  - La sucursal principal es el valor por defecto: una sucursal sin credenciales propias hereda las de la principal (`BranchCredentialResolver`); la principal no se "quita", se edita. Los eventos automáticos de recepción pasan a ser por sucursal (`BranchReceptionEvents`, sin fila hereda los de la principal) y el servicio de eventos usa los de la sucursal que recibió el documento. Desaparecen `minsalud-config`, `ihce-config` y `reception-settings` del Client en tenant; en el portal, con "Todas" se trabaja sobre la principal.
  - tenant-web: el selector "Aplica a" lista solo sucursales (la principal marcada como valor por defecto) y hay una tarjeta nueva de eventos automáticos. El SMTP (tenant-web `ClientEdit` y `Branding`, client-web `TemplateSettings`) es un solo componente compartido, `SmtpSettingsCard`, con el lapicito incluido.
  - Pruebas: migración con datos (20), API del tenant y del portal (56), resolvedor (24), cobro (26), regresión de las fases 5 (91) y anteriores (t-api, t-hmac, t-create, t4), 21 de interfaz de tenant-web y 232 e2e del portal.
- **Lapicito en los formularios de credenciales existentes:** hecho y probado; **pendiente de desplegar** (solo tenant-web y client-web, sin backend ni migración). Proveedor de documentos (Dataico) y SMTP del cliente en `ClientEdit`, SMTP del tenant en `Branding` y SMTP del cliente en `TemplateSettings` arrancan bloqueados y sin autocompletado; el lapicito los habilita, Guardar solo existe desbloqueado, vuelven a bloquearse tras guardar y "Probar conexión" sigue disponible. En Dataico también se bloquean los botones de proveedor. La contraseña del .p12 al subir un certificado solo lleva `autoComplete="new-password"` (no es una credencial guardada). Pruebas: 11 de interfaz de tenant-web contra la API real, 1 e2e nueva del portal y 231 e2e en total. No verificado: los botones de proveedor bloqueados (la base de prueba no tiene proveedores habilitados) ni el campo de la contraseña del .p12 (no apareció en la pantalla de prueba).
- **Fase 6 (credenciales por sucursal: MinSalud, IHCE y buzón de recepción):** implementada y probada; **pendiente de desplegar** (migración `AddBranchCredentials`, solo crea tablas; orden fel-migrator, fel-api-tenant, fel-api-client, fel-worker, tenant-web, client-web, de uno en uno).
  - Tres tablas 1:1 con la sucursal (`BranchMinSaludCredentials`, `BranchIhceCredentials`, `BranchReceptionMailboxes`): sin fila la sucursal hereda las del Client (que siguen siendo el valor por defecto, nada se copia); con fila, ésta reemplaza por completo a las del Client y no arrastra claves heredadas. `BranchCredentialResolver` da las credenciales efectivas.
  - RIPS: `rips/emit` toma la sucursal de la llave HMAC y usa sus credenciales. Buzón: el worker revisa el del Client (lo que llegue queda en la principal) y el de cada sucursal activa que tenga el suyo (lo que llegue queda en ella). Las claves van cifradas.
  - IHCE no tenía endpoints ni pantalla: ahora tiene valor por defecto del Client (`ihce-config`) y propio por sucursal. La llave de suscripción APIM se guarda cifrada.
  - Tenant: `TenantBranchCredentialsController` (GET/PUT/DELETE por grupo, y prueba de conexión del buzón). Portal: `reception-settings` según la sucursal elegida ("Todas" = buzón del cliente; una sucursal = el suyo), `DELETE` para volver a heredar y `onlyAutoSend` para guardar los eventos automáticos sin crear un buzón propio.
  - Interfaz: tarjetas MinSalud, IHCE y buzón en tenant-web con selector "Aplica a" y lapicito (formularios bloqueados y sin autocompletado); buzón del portal con lapicito y mensaje de alcance.
  - Pruebas: 45 de API (tenant y portal), 16 del resolvedor (buzones activos, herencia, aislamiento entre clientes, cascada), 16 de interfaz de tenant-web, 4 e2e nuevas del portal; regresión de la fase 5 (91), t-api/t-hmac/t-create/t4 y 226 e2e. **No verificado de punta a punta:** el envío real de un RIPS con la llave de una sucursal (la validación de la solicitud corta antes del MUV); el resolvedor y su cableado sí están probados.
- **Fase 5 (tenant-web: sucursales y usuarios, y cobro por sucursal):** implementada y probada; **pendiente de desplegar** (con migración `AddBranchBilling`: respaldo de FelDb antes; orden fel-migrator, fel-api-tenant, fel-api-client, fel-api-superadmin, fel-worker, tenant-web).
  - Migración: `Branch` gana `SubscriptionRate`, `PricePerDocument` y `DeactivatedAt`; la principal recibe la tarifa del Client (idempotente) y `ClientIntegratorBilling` pasa a `(BranchId, IntegratorId)` con las filas existentes ligadas a la principal. Reversible y sin cambios de modelo pendientes.
  - Servicios compartidos `ClientUserAdminService` y `ResolutionBranchService` (portal y tenant aplican las mismas reglas). El tenant gestiona sucursales (`TenantBranchesController`: crear, editar, desactivar, reactivar, llaves por sucursal), usuarios (`TenantClientUsersController`) y resoluciones por sucursal; la tarifa por integrador es por sucursal. Salen del tenant: `portal-user`, `generate-key`, llaves y cuota mensual del cliente.
  - Cobro: modo por usuario cuenta sucursales activas de clientes activos, prorrateadas por días hasta `DeactivatedAt`; modo por documento cobra cada documento a la tarifa de su sucursal (o su override de integrador) con las bolsas del cliente cubriendo primero los documentos más antiguos (`ComputeBagConsumptionPerDocument`, idéntica a la anterior con tarifas iguales). `PriceCharged` al emitir y el inicio del portal usan la tarifa de la sucursal. Reactivar una sucursal limpia `DeactivatedAt` y vuelve a cobrar desde su fecha de creación (no hay descuento por el tramo inactivo).
  - tenant-web: pestañas Sucursales (con llaves y tarifas por integrador) y Usuarios del portal, sucursales en Resoluciones; se retiran la cuota mensual y los bloques de llaves, usuario único y tarifa por integrador de las pestañas anteriores.
  - Pruebas: 26 de cobro con cifras a mano, 91 de la API del tenant, regresión t-api/t-hmac/t-create/t4 y 226 e2e del portal; verificado en el navegador.

- **Fase 4 (portal con sucursal, usuarios y rol Facturador):** implementada y probada; **pendiente de desplegar** (sin migración: API del cliente, API del tenant y client-web).
  - Backend: `GET /client/session` (rol, sucursales y catálogos de roles y tipos de nota), `ClientUsersController` (crear con invitación, editar, reenviar, desactivar y reactivar,
    con las salvaguardas de no desactivarse a uno mismo y de que siempre quede un Administrador con acceso a todas las sucursales), sucursales por resolución (`PUT {id}/branches`),
    numeración propia por sucursal (`note-numberings`) y `[AllowAllBranches]` para que la configuración del cliente se pueda guardar con "Todas" elegido. Las rutas nuevas
    se agregaron a las del portal en el middleware HMAC. El "usuario del portal" del tenant pasa a ser el Administrador más antiguo.
  - Portal: `SessionContext`, selector de sucursal en el header (`x-branch-id`), menú y rutas por rol, pantalla Usuarios, y sucursales y numeración propia en Resoluciones.
  - Migración `AddDocumentLegends` restaurada (registrada ante EF y con las columnas de Clients que le faltaban): una base nueva construida solo con migraciones queda
    idéntica al esquema de producción (733 columnas, 111 índices, 58 tablas).
- **Fase 3 (dirección de la sucursal y numeración de notas):** implementada, probada y **desplegada en producción** (mismo esquema, un servicio a la vez).
  - `Branch` gana `Address`, `City`, `CityCode`, `Phone` y `Email`, todos opcionales: sin dirección propia la sucursal hereda la del
    Client (no se copia nada, así no se desactualiza). `EmitterLocation.For(client, branch)` usa dirección, ciudad y código de ciudad
    juntos y deja teléfono y correo caer al Client. Los mappers de la DIAN (`DianDocumentMapper`, `PayrollDocumentMapper`) y de PDF
    reciben la ubicación como parámetro obligatorio; los PDF agregan `SucursalNombre` y `SucursalCodigo`. Con Dataico no se manda
    dirección del emisor (la toma la cuenta de Dataico); en el XML de la DIAN solo cambia `PhysicalLocation`.
  - `NoteNumbering (ClientId, BranchId?, Kind, Prefix?, NextNumber?)` reemplaza los contadores de notas de `Client`: una fila compartida por
    cliente y tipo (BranchId nulo) y, si una sucursal lo necesita, una propia con su prefijo (único por cliente y tipo).
    `ResolutionNumbering.ClaimNextNoteAsync` usa la fila de la sucursal si existe y si no la compartida; `note-counters` (portal y tenant)
    conserva su respuesta y escribe en las filas compartidas. Las columnas viejas de `Client` quedan marcadas obsoletas hasta la fase 7.
  - **Despliegue:** tras el último servicio hay que sincronizar los contadores (dejar en `NoteNumberings` el mayor entre la fila y la
    columna vieja de `Client`), porque un servicio viejo que emita una nota durante el despliegue solo incrementa la columna vieja.
- **Fase 2 (sucursal en el backend, llaves y roles):** implementada, probada y **desplegada en producción junto con la fase 1** (un servicio a la vez: migrador, API del cliente, del tenant, de integración, de superadmin y Worker).
  - `BranchContext` + `ClientPortalFilter` + `ClientPortalControllerBase`: cliente, sucursal (`x-branch-id`, `all` solo lectura) y rol
    por petición; los 14 controladores del portal dejan de repetir `GetCurrentClientId()`.
  - Roles exigidos por endpoint con `[ClientRole]` (Administrador / Facturador).
  - Documentos, recibidos y resoluciones filtrados por sucursal; toda creación asigna `BranchId`; resoluciones nuevas quedan ligadas a la sucursal.
  - Llaves de API en `Branch` (migración `AddBranchApiKeys`, copia las del cliente sin cambiar valores) y `ApiCredentialResolver`
    único para los 4 middlewares HMAC; la sucursal viaja en `UblInvoiceData` hasta el Worker.
  - Endurecimiento: se eliminan `POST api/Client/{id}/apikey` y `.../testset` (solo pedían una firma HMAC de cualquier cliente, sin
    verificar que el `clientId` fuera el de la llave), la carga de certificado exige que la llave sea del mismo cliente, y las
    propiedades secretas de `Client`, `Branch` y `ClientUser` no se serializan.
  - Las columnas viejas de llaves de `Client` siguen hasta la fase 7 (idénticas a las de la sucursal principal).
  - Pendiente para la fase 4: el dashboard del Facturador no recibe el consumo (el portal ya tolera `consumption: null`).
- **Fase 1 (modelo y migración):** implementada, probada y **desplegada en producción**.
  Migración `AddBranches`: crea `Branches`, `ResolutionBranches` y `ClientUserBranches`; agrega `ClientUsers.Role` y
  `AllBranches`, y `BranchId` (nulable) en `Documents` y `ReceivedDocuments`; rellena de forma idempotente la sucursal
  principal por cliente y asigna a ella documentos, recibidos, resoluciones y usuarios.
