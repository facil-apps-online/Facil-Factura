# RADIAN: consecutivos repetidos en SiliMedical

Investigacion realizada el 2026-10-29. Este documento conserva el diagnostico,
las evidencias y las tareas necesarias para retomar el desarrollo sin repetir la
investigacion.

## Estado

Diagnostico inicial terminado. La correccion visual ya fue desplegada y la
primera proteccion de idempotencia quedo implementada y compilada localmente.
No se han modificado historicos de eventos ni datos RADIAN de produccion.

## Avance de la sesion

- Inventario actualizado: 169 intentos, 53 `SENT`, 8 `DUPLICATE` y 108
  `REJECTED`.
- El portal ya muestra `DUPLICATE` como `Duplicado / error` en rojo; no se
  presenta como enviado correctamente.
- `ReceptionEventService` ahora toma un bloqueo distribuido por cliente,
  documento y tipo de evento durante la operacion externa. Antes de llamar al
  proveedor revisa el ultimo intento: `SENT`, `DUPLICATE` y `PENDING` no se
  vuelven a enviar; `REJECTED` sigue siendo reintentable.
- La proteccion fue compilada con 0 errores y 7 advertencias existentes.
- Cuando Dataico responde duplicado, el servicio ahora consulta el evento
  original; si recupera la respuesta, reemplaza el mensaje de duplicado,
  conserva CUDE/identificadores/URLs y guarda el intento como `SENT`. Si la
  consulta no responde, conserva `DUPLICATE` para evitar confirmar sin evidencia.
- La conciliacion de historicos sigue separada del cambio operativo y aun esta
  pendiente de ejecucion controlada.
- La rutina de reconciliacion sin reenvio fue desplegada en API y worker el
  2026-10-29. Ambos servicios quedaron activos y el worker registra actividad
  normal. El migrador sigue fallando por el problema previo de migraciones,
  pero esta funcionalidad no requiere cambios de esquema.
- API y worker fueron desplegados y verificados activos el 2026-10-29. El
  script de despliegue reporto fallo porque intenta ejecutar el migrador
  pendiente; los servicios se levantaron explicitamente sin el migrador y no
  se ejecutaron cambios de esquema.

## Diagnostico realizado

### 1. Revision del codigo

Se revisaron estos puntos:

- `src/Fel.Infrastructure/Dian/ReceptionEventService.cs`
  - El evento RADIAN de Dataico obtiene el numero con `PeekNextAsync`.
  - El numero solo se avanza despues de una respuesta exitosa.
  - El bloqueo actual es `SemaphoreSlim` en memoria, indexado por cliente.
  - Si Dataico responde duplicado, el intento local se guarda como `DUPLICATE`,
    conserva el consecutivo y avanza la numeracion.
- `src/Fel.Infrastructure/Services/ReceptionEventNumberingService.cs`
  - Existe `ClaimNextAsync` con actualizacion atomica, pero el flujo RADIAN no lo
    utiliza; usa `PeekNextAsync` y `AdvanceIfCurrentAsync`.
- `src/Fel.Api.Client/Controllers/ReceivedDocumentsController.cs`
  - El portal permite disparar eventos manuales.
  - La carga de documentos y el worker pueden disparar eventos automaticos.
- `src/Fel.Infrastructure/Dian/ReceptionEmailPollerService.cs` y
  `src/Fel.Worker/ReceptionEmailWorker.cs`
  - El worker recibe documentos por correo y dispara eventos automaticos.
- `apps/client-web/src/pages/ReceivedDocumentsPage.tsx`
  - `DUPLICATE` se muestra actualmente en verde como `Recibido`.
  - El historial muestra los intentos individuales.

### 2. Revision de produccion

Cliente revisado: SiliMedical SAS. La consulta fue de solo lectura.

- Integrador: Dataico, ambiente de produccion.
- Prefijo RADIAN: `EV`.
- Siguiente consecutivo local al momento de la revision: `EV4414`.
- Intentos locales con consecutivo: 169.
- Consecutivos utilizados: 50.
- Consecutivos repetidos: 15.
- Consecutivos usados para mas de un documento: 14.
- Intentos `SENT`: 53.
- Intentos `DUPLICATE`: 8.
- Intentos `REJECTED`: 108.

Casos recientes confirmados:

- `EV4411`: un evento fue aceptado y tres intentos posteriores reutilizaron el
  mismo consecutivo; Dataico los respondio como duplicados.
- `EV4413`: un evento fue aceptado y tres intentos posteriores reutilizaron el
  mismo consecutivo; Dataico los respondio como duplicados.
- Los intentos duplicados comparten el identificador del evento aceptado en
  Dataico, lo que confirma que no son solamente un problema de visualizacion.
- `EV4412` fue aceptado como evento unico.

De las ocho filas `DUPLICATE`:

- una es reconciliable con el mismo documento y tipo de evento;
- seis son conflictos confirmados de consecutivo entre documentos o tipos de
  evento;
- una requiere consulta adicional a Dataico porque no tiene identificador del
  proveedor.

Los 104 rechazos explicitos se conservan como intentos fallidos y no como casos
ambiguos, salvo que una consulta futura del proveedor demuestre lo contrario.

### Evidencia de conciliacion consultada posteriormente

#### Rango y huecos de numeracion

El inventario local actualizado muestra primer consecutivo `EV4364`, ultimo
`EV4414`, 51 consecutivos distintos y 0 huecos dentro del rango. La secuencia
local es continua; el problema observado es reutilizacion de consecutivos en
varios intentos/documentos, no faltantes locales.

Se confirmaron nuevamente las ocho filas `DUPLICATE` y se compararon con todos
los intentos locales que usan los consecutivos `EV4364`, `EV4409`, `EV4411` y
`EV4413`:

- `EV4364 / 030 / GL072258361`: existe un `SENT` del mismo documento,
  `GL072258361`, con el mismo `ProviderUid` y CUDE. Es un caso recuperable;
  el response del evento `SENT` es la evidencia a copiar al intento duplicado.
- `EV4411 / 030` y `EV4411 / 032`: el `ProviderUid` y CUDE coinciden con el
  evento `SENT` de `8300628283`, pero también hay intentos para `FE12866`.
  Estos últimos son conflictos de documento y no se deben marcar como enviados
  sin una consulta específica a Dataico.
- `EV4413 / 030` y `EV4413 / 032`: el `ProviderUid` y CUDE coinciden con el
  evento `SENT` de `T18579`, pero también hay intentos para `FEB44830`.
  Estos últimos son conflictos de documento y requieren la misma validación.
- `EV4409 / 030 / MULT25120`: no tiene `ProviderUid` ni CUDE local; el
  consecutivo aparece en otro evento (`EV4409 / 033 / FVE3470`). Es ambiguo y
  no se debe reconstruir por copia local.

No se modificaron filas en esta consulta. La siguiente acción debe consultar el
evento original en Dataico para los casos recuperables y ambiguos, conservar el
response completo y actualizar solo cuando la identidad del documento y evento
quede confirmada.

### Prueba y reversión de EV4364/030

Se probó la reconstrucción del caso exacto `EV4364/030/GL072258361` copiando
desde el intento local `SENT`. La verificación mostró que ese registro no tiene
`ProviderResponse` almacenado (`DATALENGTH = 0`), por lo que no era evidencia
suficiente para reconstruir el response de Dataico. El cambio fue revertido y
el intento permanece como `DUPLICATE`. No se confirma ningún duplicado como
`SENT` hasta recuperar el response real del proveedor.

## Tareas de desarrollo

### RADIAN-001 - Reconstruir el historial de SiliMedical

- Consultar Dataico para los consecutivos repetidos y casos sin identificador.
- Comparar documento, tipo de evento, consecutivo, CUDE, identificador externo y
  fecha.
- Clasificar cada intento como `SENT`, `ALREADY_EXISTS`, `CONFLICT`, `REJECTED` o
  `UNKNOWN`.
- Generar un reporte de reconciliacion y conservar la evidencia de cada decision.

### RADIAN-002 - Corregir la semantica de estados

- Mantener `SENT` como unico estado de exito confirmado.
- No presentar `DUPLICATE` como enviado o recibido correctamente.
- Introducir una distincion entre duplicado del mismo documento/evento y conflicto
  de consecutivo con otro documento/evento.
- Definir la politica para `UNKNOWN` y su recuperacion manual.

### RADIAN-003 - Corregir la interfaz del portal

- Cambiar `DUPLICATE` a estado de error o advertencia, no verde.
- Mostrar el mensaje de Dataico y el consecutivo involucrado.
- Mantener el historial de intentos sin ocultar el evento aceptado original.
- Evitar que un conflicto pueda reintentarse sin reconciliacion o confirmacion.

### RADIAN-004 - Reserva atomica de consecutivos

- Usar una reserva atomica para cada envio, no `PeekNextAsync` seguido de avance.
- Eliminar la dependencia exclusiva del `SemaphoreSlim` en memoria.
- Coordinar API, worker, jobs y cualquier otra instancia mediante base de datos o
  un mecanismo distribuido.
- Definir que ocurre con el consecutivo reservado cuando Dataico rechaza, expira o
  responde de forma desconocida.

### RADIAN-005 - Idempotencia por documento y tipo de evento

- Crear una clave estable por cliente, documento recibido, tipo de evento y
  operacion.
- Antes de enviar, detectar un intento en progreso o un resultado ya confirmado.
- Ante timeout o reinicio, consultar al proveedor antes de volver a enviar.
- No reutilizar un consecutivo que el proveedor ya asocio a otro evento.

### RADIAN-006 - Reconciliacion y observabilidad

- Guardar operacion, intento, consecutivo, proveedor, respuesta y correlation id.
- Registrar explicitamente el resultado `UNKNOWN`.
- Crear consulta administrativa para conflictos y operaciones pendientes.
- Permitir reparar la asociacion local sin borrar el historial original.

### RADIAN-007 - Cobertura transversal de envios

- Aplicar el mismo contrato a eventos manuales y automaticos.
- Cubrir Dataico y emision nativa directa a la DIAN.
- Revisar worker, API, colas, jobs y reintentos.
- Integrar este trabajo con el backlog general de idempotencia y concurrencia para
  todos los eventos de envio.

### RADIAN-008 - Pruebas

- Doble clic y doble pestaña.
- API y worker concurrentes.
- Reintento despues de timeout.
- Reinicio entre aceptacion externa y guardado local.
- Respuestas `SENT`, `DUPLICATE`, `REJECTED`, 429, 504 y desconocida.
- Verificar que solo `SENT` se muestre en verde.
- Verificar que los reenvios intencionales queden diferenciados de los reintentos
  automaticos.

## Orden recomendado

1. Completar `RADIAN-001` sin modificar produccion.
2. Acordar estados y reglas de reconciliacion (`RADIAN-002`).
3. Construir reserva atomica e idempotencia (`RADIAN-004` y `RADIAN-005`).
4. Adaptar portal, API y worker (`RADIAN-003`, `RADIAN-006` y `RADIAN-007`).
5. Ejecutar pruebas de concurrencia y regresion (`RADIAN-008`).
6. Desplegar migracion, APIs, worker y portal de forma controlada.


## Revision del 2026-10-30

La implementacion del 2026-10-29 tenia errores y se corrigio:

- Un `DUPLICATE` se marcaba `SENT` copiando la consulta del evento por
  consecutivo, que puede ser de otra compra. Se elimino esa consulta.
- El control de reenvio miraba solo el ultimo intento: un `SENT` seguido de un
  `DUPLICATE` o `REJECTED` se reenviaba. Ahora cualquier `SENT` del documento y
  tipo de evento se devuelve sin llamar a Dataico.
- Un `PENDING` huerfano bloqueaba el evento para siempre. Con el lock tomado un
  `PENDING` es una operacion caida: se cierra como `REJECTED` y se reintenta.
- El portal referenciaba una variable inexistente (`latest`) y rompia la
  descarga del PDF del documento recibido.

### Los dos tipos de "duplicado" (confirmado en produccion)

- `Ya tienes evento de recepción con esa compra y tipo de evento`: el evento ya
  existe para la compra. Queda `DUPLICATE`, se muestra como "Registrado en
  Dataico" (sin comprobante local) y no se reenvia.
- `El documento 'N' ya esta aceptado en la Dian` (`code 002`): choque de
  consecutivo; el evento de esta compra NO quedo registrado. Ahora es
  `REJECTED` reintentable y conserva la respuesta de Dataico.
- En ninguno de los dos casos se asigna consecutivo ni se avanza la numeracion
  (antes quedaba un hueco local que Dataico obligaba a reutilizar).

### Estado de los datos

- 13 filas `DUPLICATE` en total; todas menos una tienen un `SENT` posterior del
  mismo documento y evento. La unica sin `SENT` es SiliMedical `FE12860/033`,
  del tipo "ya registrado". No se requiere correccion de datos.
- Los 13 rechazos del 2026-10-22 con "El número ... es inválido ... Vamos a
  usar: '4364'" ocultaban el error real (ciudad invalida). Desde `9c45ae4`
  (2026-10-24) se reintenta con el numero sugerido y se guarda el error real.
- Cinco intentos fueron editados a mano en produccion el 2026-10-29 con mensajes
  "Corregido contra log de Dataico..." y sin la respuesta original
  (`FE12860`, `FPO15156272`, `AG397`; `EV4423`-`EV4425`).
- El mensaje del `DUPLICATE` de `GL072258361/030` tambien fue sobrescrito.

### Pendiente

- Recuperar el CUDE de `FE12860/033` si se necesita (panel de Dataico).
- Avanzar la numeracion local cuando el ciclo de disponibilidad salta numeros
  ocupados en Dataico.
