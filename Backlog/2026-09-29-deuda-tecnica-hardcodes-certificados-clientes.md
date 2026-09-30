# Deuda técnica pendiente: hardcodes y flujo de clientes/certificados

Revisión realizada después de las pruebas Sandbox del portal de tenants. Este documento reúne lo que conviene reparar en una sesión dedicada; no se modifica el comportamiento actual en esta sesión.

## Prioridad alta

### 1. Configuración de ambientes y URLs

Hay URLs y valores de ambiente definidos directamente en código o con valores de desarrollo:

- `apps/tenant-web/src/pages/TemplateEditor.tsx`: fallback `http://localhost:5000` para reportes.
- `src/Fel.Api.Tenant/Program.cs`: servidor Swagger con `https://api.facil-factura.pro`.
- Archivos `launchSettings.json` y `.http` con puertos locales que pueden confundirse con configuración real.
- Scripts de despliegue con IP/host del VPS.

**Pendiente:** centralizar por ambiente en `appsettings.*`, variables de entorno y archivos `.env` del build. El código no debería conocer dominios, IPs ni puertos de producción.

### 2. Clave de Google Maps

`ClientFormFields.tsx` contiene un fallback con una clave de Google Maps (`AIza...`), aunque sea una clave de prueba.

**Pendiente:** eliminar cualquier clave del código fuente, exigir `VITE_GOOGLE_MAPS_API_KEY` en cada ambiente y validar el arranque cuando falte. Revisar restricciones de dominio, cuotas y rotación de la clave actual.

### 3. Identidad del tenant basada en `x-tenant-id`

Varios controladores repiten la lectura del header `x-tenant-id` y existen comentarios que describen el riesgo de aceptar un tenant enviado por el cliente.

**Pendiente:** obtener siempre el tenant desde la sesión/JWT y encapsularlo en un servicio único (`ICurrentTenant`). Dejar el header solo para compatibilidad controlada o eliminarlo. Agregar pruebas que intenten acceder a un cliente de otro tenant.

### 4. Datos del certificado duplicados en frontend y backend

El mapeo de campos Viafirma está duplicado en `ClientEdit.tsx` y `TenantCertificatesController.cs`: `district`, `departament`, `identityType`, `countryCode`, `dnAlternativo1`, `dnAlternativo2`, nombres del representante, etc.

También se duplican:

- códigos PN/PJ;
- `CO` como país por defecto;
- `RM` como tipo de organización;
- nombres y opciones de tipos de organización;
- reglas para decidir si un perfil es corporativo o individual.

**Pendiente:** crear un catálogo/mapeo de certificados en backend, exponer al frontend únicamente los campos editables y construir el payload final en un servicio de aplicación. La API debe ser la única fuente de verdad.

### 5. Valores sensibles o críticos con fallback silencioso

El flujo usa valores por defecto como `CO`, `PJ`, `RM`, `IDC`, ciudad como departamento y datos de organización como fallback de representante. Ya se detectó un caso real donde `null` y cadena vacía se comportaron diferente y Viafirma rechazó la solicitud.

**Pendiente:** normalizar `null`, vacío y espacios en un único lugar; distinguir valor faltante de valor por defecto; validar antes de generar CSR; y mostrar exactamente qué dato falta. No usar la ciudad como departamento sin marcarlo como inferido.

## Prioridad media

### 6. Identificadores y reglas de negocio hardcodeadas

Se encontraron reglas embebidas en código como:

- GUID fijo del integrador nativo en `TenantClientsController`.
- `COP`, `CO`, agencia DIAN `195` y textos UBL repetidos.
- Valores numéricos de ambiente enviados desde el frontend (`1` Sandbox y `2` Production).
- Detección PN/PJ por texto en el código del perfil (`INDIVIDUAL`, `CORPORAT`, `PN`, `PJ`).
- `RM` y catálogo Viafirma repetido en el formulario.

**Pendiente:** mover catálogos, GUID, códigos de ambiente, país, moneda y códigos DIAN a constantes tipadas o configuración/catálogos de base de datos, evitando literales repartidos.

### 7. Repetición de autenticación y manejo de errores

Muchos controladores implementan su propia versión de `GetCurrentTenantId()`, lanzan excepciones por headers faltantes o devuelven `Unauthorized` desde bloques `catch` generales.

**Pendiente:** middleware/filtro único para autenticación, autorización y tenant context; errores con códigos consistentes; no convertir excepciones internas en `401` ni ocultar la causa real.

### 8. Formularios con estado `any` y contratos no tipados

`ClientEdit.tsx`, `Clients.tsx` y `ClientFormFields.tsx` comparten el cliente como `any`. Esto permitió que campos nuevos, nombres externos de Viafirma y valores vacíos se desalinearan sin error de compilación.

**Pendiente:** definir `ClientDto`, `UpdateClientRequest`, `CertificateOptionsDto` y `CertificateFormValues` compartidos; eliminar `any` del flujo de clientes/certificados.

### 9. Migraciones y despliegues

El incidente reciente mostró que API y migrador pueden quedar desfasados. Además, una migración manual no fue detectada inicialmente por el bundle al faltar su descriptor de EF.

**Pendiente:** pipeline que compile, liste migraciones pendientes, genere/aplique el bundle y verifique el estado antes de reiniciar API. Agregar una comprobación automática de que la migración esperada existe en el bundle.

## Pruebas pendientes

- Prueba de integración para PN y PJ con payload manipulado: el backend debe ignorar los campos de certificado enviados por el navegador.
- Prueba con representante sin país, país vacío y país `CO`.
- Prueba con RUT de una hoja y RUT completo.
- Prueba de carga de RUT de otro NIT sobre un cliente existente.
- Prueba de perfiles Viafirma faltantes o con nombres externos diferentes.
- Prueba de sesión expirada y error de `billing-metrics`, verificando que no cierre sesión por un error interno.
- Prueba de despliegue API + migrador + portal en ese orden.

## Orden recomendado para la próxima sesión

1. Centralizar configuración y eliminar claves/URLs del código.
2. Consolidar el contexto de tenant y la autenticación.
3. Extraer catálogos y mapeos de Viafirma a backend/configuración.
4. Tipar los DTO del cliente y certificados.
5. Automatizar migraciones/despliegues.
6. Crear las pruebas de integración del flujo Sandbox.

