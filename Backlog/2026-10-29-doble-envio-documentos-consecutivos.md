# Doble clic o doble envio de documentos

Investigacion realizada el 2026-10-29. Este documento conserva los pasos, la
evidencia y las tareas para retomar el desarrollo del control de doble envio.
El alcance debe ser transversal y coincide con el pendiente general de
idempotencia y concurrencia en todos los eventos de envio.

## Estado

Diagnostico terminado. Desarrollo en curso. No se hicieron cambios en datos de
produccion durante esta revision.

Avance iniciado:

- El portal agrega una guarda inmediata con `ref` para bloquear dos clics
  sincronicos sobre el mismo documento.
- La API agrega `SqlAppLock`, un lock distribuido de SQL Server por documento,
  para publicar factura, documento soporte y nomina sin carreras entre procesos.
- El servicio de eventos RADIAN agrega un lock distribuido por cliente para que
  API y worker no reserven simultaneamente el mismo consecutivo.

La tarea no se marca como hecha todavia: falta la idempotencia persistida, la
reconciliacion posterior a timeout y la verificacion de despliegue.

Revision del 2026-10-30:

- El claim del documento se hacia antes de varias validaciones; un `BadRequest`
  o una validacion del integrador lo dejaba `IN_PROGRESS` y, al vencer el lease,
  `UNKNOWN` sin salida. Afecto a dos facturas en produccion (SUSANA TABA DE
  PESCADOR, resolucion FE agotada en 3000; NOJAU S.A.S.). Se verifico en Dataico
  que ninguna llego (FE3001/FE3002 y FEV25/FEV26 no existen).
- Ahora el claim se hace justo antes de llamar al proveedor
  (`ClaimSubmissionAsync`) y el envio pasa por `SendClaimedAsync`: una
  `NotSupportedException` (incluye `ResolutionExhaustedException`) libera el
  documento como `FAILED`; cualquier otra excepcion lo deja `UNKNOWN`.
- En documento soporte el claim va antes de reservar el consecutivo.
- Falta: herramienta para resolver `UNKNOWN` (consultar el proveedor y liberar o
  asociar), cubrir reenvio y la API de integracion, y la numeracion segura.

## Casos revisados

### CRISTHIAN EDUARDO MOLINA ESTRADA

- En Dataico se observaron los consecutivos `3262` y `3263`.
- En `FelDb` existe `FE3263` aprobado y con identificador de Dataico.
- `FE3262` no existe como documento local.
- La numeracion local quedo en el siguiente consecutivo `3264`.

### KAREN ELIANA GARCIA ALARCON

- En Dataico se observaron los consecutivos `3252` y `3253`.
- En `FelDb` existe `FE3253` aprobado y con identificador de Dataico.
- `FE3252` no existe como documento local.
- La numeracion local quedo en el siguiente consecutivo `3256`.

Confirmacion posterior: los dos documentos de cada caso llegaron a Dataico. Por
lo tanto, el problema de estos casos no es ambiguo respecto a la aceptacion
externa: existe una perdida de persistencia o de trazabilidad local para el
primer documento. Todavia no se puede afirmar que la causa exacta haya sido
literalmente un doble clic; tambien pudo ser un reintento, dos pestañas, timeout
o una caida del proceso despues de la aceptacion externa.

## Diagnostico realizado

### Frontend

Se reviso `apps/client-web/src/pages/DocumentsPage.tsx`:

- Existe el estado `publishingId` y el boton se deshabilita visualmente.
- `handleSaveDraft` tiene una guarda inmediata contra doble ejecucion.
- `handlePublishInvoice` no tiene una guarda equivalente al inicio del handler.
- Dos clics muy rapidos pueden entrar antes de que React actualice el estado.
- El bloqueo del navegador no protege dos pestañas ni solicitudes repetidas.

### Backend

Se reviso `src/Fel.Api.Client/Controllers/InvoiceController.cs`:

- El endpoint de publicacion acepta solicitudes concurrentes mientras el
  documento siga en `DRAFT` o `REJECTED`.
- No existe un claim atomico o una operacion idempotente por documento antes de
  llamar al proveedor.
- El proveedor externo se invoca antes del `SaveChangesAsync` final.
- Si el proveedor crea el documento y el proceso falla antes del guardado local,
  el documento queda en Dataico sin correspondencia en `FelDb`.

Se reviso `src/Fel.Infrastructure/Dataico/DataicoSubmissionProvider.cs`:

- Cuando falta el numero, el flujo consulta el siguiente candidato con
  `PeekNextNumberAsync`.
- Se verifica si el numero ya existe en Dataico, pero dos solicitudes pueden
  consultar el mismo candidato antes de que alguna lo confirme.
- El numero se confirma despues del envio; no es una reserva idempotente.

## Tareas de desarrollo

### SEND-001 - Reconstruir los casos existentes — HECHO (2026-10-29)

- Reconciliar los dos documentos de Cristihan y los dos de Karen que ya fueron
  confirmados como recibidos por Dataico.
- Consultar Dataico y `FelDb` por los consecutivos afectados.
- Asociar cada documento externo con su documento local, CUFE y respuesta DIAN.
- Determinar si el faltante local fue una caida posterior a la aceptacion, un
  reintento o una colision de numeracion; la aceptacion externa ya esta
  confirmada para estos cuatro documentos.
- Documentar cualquier documento externo que requiera reconstruccion local.

Resultado:

- Cristihan `3262` y `3263`: ambos confirmados en Dataico; `FE3263` tiene
  correspondencia local y `FE3262` no.
- Karen `3252` y `3253`: ambos confirmados en Dataico; `FE3253` tiene
  correspondencia local y `FE3252` no.
- No se reemitio ninguno de los cuatro documentos.
- La consulta tecnica directa posterior devolvio `490 session-expired`; por eso
  no se inventaron CUFE/UUID para los dos faltantes. La confirmacion externa y
  la evidencia local quedan registradas en esta ficha.
- La creacion de registros locales faltantes requiere una operacion posterior
  de reconstruccion con XML/CUFE/UUID confirmados; se ejecuto sin nuevo envio el
  2026-10-29.

Reconstruccion ejecutada:

- `FE3262` se creo copiando el detalle local de `FE3263`, pero con el CUFE y
  UUID reales consultados en Dataico.
- `FE3252` se creo copiando el detalle local de `FE3253`, pero con el CUFE y
  UUID reales consultados en Dataico.
- Ambos quedaron `APPROVED`, con una linea, cliente, totales, forma de pago y
  referencias de sucursal coincidentes con su fuente local.
- Se copiaron tambien retenciones, impuestos adicionales, retenciones generales
  y observaciones; las fuentes no tenian filas de esos tipos.
- No se copiaron CUFE, UUID, QR ni respuesta externa de `3263`/`3253`.
- La operacion fue transaccional y queda reproducible en
  `scripts/reconstruct-external-invoices.sql`.

### SEND-002 - Guarda inmediata en el portal

- Agregar una guarda atomica en frontend para publicacion y reenvio.
- Usar un `ref` o mecanismo equivalente, no solo estado de React.
- Bloquear la misma accion por documento mientras esta en curso.
- Cubrir facturas, documentos soporte, notas de ajuste y demas botones de envio.

### SEND-003 - Claim atomico en API

- Reclamar el documento antes de llamar a Dataico o a la DIAN.
- Impedir que dos solicitudes concurrentes publiquen el mismo documento.
- Definir lease, expiracion y recuperacion para no dejar documentos bloqueados.
- Aplicar el control a publicar, reenviar y reprocesar.

### SEND-004 - Idempotencia por operacion

- Crear una clave estable por documento, tipo de operacion y proveedor.
- Persistir el intento antes de la llamada externa.
- Si llega la misma operacion otra vez, devolver el resultado existente o el
  estado `IN_PROGRESS`, sin volver a enviar.
- Diferenciar reintento automatico de reenvio intencional.

### SEND-005 - Reconciliacion posterior al envio

- Ante timeout, reinicio o respuesta desconocida, consultar al proveedor antes
  de repetir.
- Buscar por numero, CUFE, identificador externo y otros datos disponibles.
- Asociar el resultado externo al documento local cuando exista.
- Marcar `UNKNOWN` cuando no sea posible confirmar, nunca asumir exito.

### SEND-006 - Numeracion segura

- Revisar todos los flujos de reserva y confirmacion de numeracion.
- Evitar patrones `Peek` + envio + `Commit` sin proteccion de concurrencia.
- Asegurar que una reserva no sea reutilizada por otra solicitud.
- Definir como se recuperan huecos cuando el proveedor acepto pero no se guardo
  el resultado local.

### SEND-007 - Observabilidad

- Registrar `OperationId`, documento, proveedor, intento, numero, correlation id,
  estado y respuesta sanitizada.
- Registrar el momento de claim, envio externo, respuesta y persistencia local.
- Crear consulta administrativa para operaciones `IN_PROGRESS` y `UNKNOWN`.
- Mantener el historial de intentos sin borrar evidencia.

### SEND-008 - Pruebas de concurrencia

- Doble clic rapido.
- Dos pestañas o dos usuarios sobre el mismo documento.
- Reintento HTTP despues de timeout.
- Caida entre aceptacion externa y `SaveChangesAsync`.
- Reinicio de API o worker durante el envio.
- Dataico y emision nativa directa a la DIAN.
- Verificar que una operacion produzca un solo documento externo y un solo
  resultado local consistente.

## Orden recomendado

1. `SEND-001` queda cerrado; continuar con `SEND-003` y `SEND-004`.
2. Diseñar estados, lease e idempotency key (`SEND-003` y `SEND-004`).
3. Corregir reserva y confirmacion de numeracion (`SEND-006`).
4. Adaptar frontend, API, workers y proveedores (`SEND-002`, `SEND-005` y
   `SEND-007`).
5. Ejecutar pruebas de concurrencia (`SEND-008`).
6. Desplegar migracion, APIs, workers y portales de forma controlada.

