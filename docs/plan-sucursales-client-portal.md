# Plan: sucursales en el portal del cliente

Estado: **fases 1, 2 y 3 en producción (5 de octubre de 2026); fase 4 implementada, pendiente de desplegar.** Ver la sección 14.

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

Ninguno por ahora.

## 14. Estado

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
