# Idempotencia y concurrencia en todos los eventos de envio

Pendiente identificado el 2026-10-29 al revisar documentos consecutivos de dos
clientes. En ambos casos el numero anterior aparecia en Dataico, pero no quedo
guardado como documento local y el siguiente numero si quedo registrado. El
patron es compatible con doble clic, doble pestaña, reintento del navegador o
una caida de la API despues de que el proveedor externo aceptara el documento.

No se implementa en esta sesion. El objetivo es resolverlo como una capacidad
transversal para cualquier operacion de envio, no como un parche exclusivo del
formulario de facturas.

## Alcance

Aplicar el mismo control a todos los eventos que puedan producir un efecto
externo o cambiar el estado de envio, incluyendo:

- Factura, documento soporte y notas de ajuste.
- Emision por Dataico.
- Emision nativa directa a la DIAN.
- Reenvio, reproceso, sincronizacion o cualquier accion equivalente desde el
  portal.
- Eventos disparados por workers, jobs, colas, webhooks o integraciones nativas,
  aunque no exista un boton en pantalla.

El control debe funcionar ante doble clic, solicitudes concurrentes, varias
pestanas, reintentos HTTP, timeouts y reinicios de proceso.

## Hallazgos actuales

- El portal deshabilita visualmente el boton despues de actualizar el estado de
  React, pero el handler de publicacion no tiene un guard inmediato. Dos clics
  muy rapidos pueden entrar antes del siguiente render.
- El backend valida que el documento este en borrador, pero dos solicitudes
  concurrentes pueden leer ese estado antes de que cualquiera lo cambie.
- El envio al proveedor externo ocurre antes del `SaveChanges` local. Si el
  proveedor acepta y el proceso falla antes de guardar, queda un documento
  externo sin correspondencia local.
- La reserva de numeracion que hace `PeekNextNumber` y luego `CommitNumber` no
  constituye por si sola una garantia contra dos envios concurrentes.

## Plan de implementacion

### 1. Inventario de eventos y contrato comun

Identificar todos los puntos que envian, reenvian, consultan o confirman un
documento ante un proveedor externo. Definir una interfaz o servicio comun de
envio con:

- `DocumentId`, tipo de documento, tenant/cliente y proveedor.
- `OperationId` y `IdempotencyKey` estables por operacion logica.
- estado local de la operacion, intento, timestamps y resultado externo.
- politica explicita para `accepted`, `rejected`, `unknown` y `in_progress`.

No se debe depender de que el proveedor soporte idempotency keys; el control
local debe ser obligatorio y el soporte externo se usa cuando exista.

### 2. Claim atomico antes del envio

Crear una transicion atomica que reclame el documento o la operacion antes de
llamar al proveedor. Una segunda solicitud debe recibir el estado actual o
esperar el resultado, nunca iniciar otro envio.

El claim debe tener lease/timeout para no dejar documentos bloqueados
permanentemente por una caida. La recuperacion debe diferenciar entre una
operacion que aun puede estar en vuelo y una operacion abandonada.

### 3. Idempotencia y reconciliacion despues del envio

Persistir el intento y la referencia externa antes y despues de la llamada,
con una estrategia segura para el caso en que el proveedor acepte pero la
respuesta no llegue. Ante timeout o reinicio:

1. consultar por numero, identificador o huella cuando el proveedor lo permita;
2. asociar el resultado externo al documento local si ya existe;
3. solo crear un nuevo intento cuando se haya descartado el anterior;
4. dejar el caso en `unknown` y visible para recuperacion manual si no se puede
   determinar el resultado.

La reconciliacion debe cubrir especialmente los huecos observados entre el
numero que existe en el proveedor y el numero siguiente que quedo localmente.

### 4. Proteccion en todos los iniciadores

- Frontend: guard inmediato por documento y accion, estado visual y manejo de
  respuesta repetida.
- API: claim atomico e idempotencia; no confiar en el frontend.
- Workers/jobs: clave de operacion, lock distribuido o claim de base de datos y
  reintentos seguros.
- Integraciones nativas: aplicar el mismo servicio antes de firmar/enviar a la
  DIAN.
- Reenvio: separar reenvio intencional de duplicacion accidental y exigir una
  nueva operacion solo cuando el estado lo permita.

### 5. Observabilidad y soporte

Registrar sin datos sensibles: `OperationId`, `IdempotencyKey`, documento,
proveedor, numero, intento, correlation id, estado anterior/nuevo, resultado
externo, HTTP status, duracion y causa de recuperacion. Crear una consulta o
vista administrativa para operaciones `in_progress` o `unknown`.

## Pruebas de aceptacion

- Dos clics consecutivos producen una sola emision.
- Dos pestañas y dos solicitudes HTTP simultaneas producen una sola emision.
- Reintento despues de timeout no duplica el documento.
- Caida entre la aceptacion externa y el guardado local permite reconciliar sin
  emitir de nuevo.
- El mismo comportamiento funciona para Dataico y para emision nativa.
- Factura, documento soporte y notas de ajuste conservan sus reglas de fecha y
  referencia, sin crear documentos duplicados.
- Un worker duplicado o reiniciado no genera un segundo envio.
- Un reenvio autorizado sigue siendo posible y queda diferenciado del reintento
  automatico.
- Pruebas de integracion con respuestas aceptada, rechazada, timeout, error 5xx
  y respuesta desconocida.

## Dependencias y despliegue

Primero cerrar el inventario de eventos y el modelo de estados. Luego construir
la persistencia/migracion y el servicio comun, adaptar cada iniciador, agregar
pruebas de concurrencia y finalmente desplegar migrador, APIs, workers y
portales en el orden correspondiente. No corregir los numeros de los casos
observados manualmente hasta contar con trazabilidad y una estrategia de
reconciliacion.

