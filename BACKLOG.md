# Backlog

Pendientes detectados durante la sesión del 2026-08-11, al hacer fiable la dualidad
entre `FelDb` y Core. Ordenado por prioridad. Cada punto incluye el porqué, porque en
varios casos el motivo no se deduce leyendo el código.

Estado del que se parte: fases 1-3 de la dualidad implementadas, desplegadas y
verificadas en producción (commits `751a675` y `93c858b`).

---

## Seguridad

### 1. El formulario de alta autocompleta credenciales de superadmin

**Dónde:** `apps/superadmin-web/src/App.tsx`, sección "Cuenta de Administrador".

Chrome rellena `adminEmail` y `adminPassword` con las credenciales guardadas del
superadmin, porque los campos se parecen a un login. Es persistente: durante las
pruebas se vaciaron dos veces y volvió a rellenarlos entre la limpieza y el envío.

Resultado: cada alta puede crear un `TenantUser` con el email y la contraseña del
superadmin, en un portal distinto (`tenants.facil-factura.pro`). Ocurrió dos veces
en producción durante las pruebas; ambos usuarios se borraron.

No es un artefacto de automatización: le pasa a cualquiera con un gestor de
contraseñas activo.

**Arreglo:** `autocomplete="off"` en la sección y nombres de campo que el navegador
no asocie a un login (evitar `email` / `password` a secas).

### 2. Rotar la service role key de Supabase

La clave estuvo en `appsettings.json`, fichero rastreado en un repositorio público.
**Nunca llegó a publicarse** — se movió a variable de entorno antes del primer commit
que la habría subido — pero circuló durante la sesión.

Rotarla ahora es barato y no toca código: cambiar `CORE_SERVICE_ROLE_KEY` en el
`.env` del servidor y recrear `fel-api-superadmin` y `fel-api-tenant`.

---

## Funcionalidad incompleta

### 3. No existe endpoint de borrado de tenants

**Dónde:** `src/Fel.Api.Superadmin/Controllers/SuperadminTenantsController.cs`.

Hoy borrar un tenant exige SQL contra `FelDb` más un DELETE por REST contra Core, a
mano. Se hizo así tres veces durante la sesión.

Quien lo implemente tiene que decidir la política en Core: ¿se borra la ficha, se
desactiva, o se deja huérfana? Ojo con las fichas **adoptadas**: si el tenant local se
enlazó a una ficha preexistente de Core, borrarla destruiría datos ajenos al alta.
`CoreTenant.Adopted` distingue ambos casos.

### 4. `ContactEmail` sigue sin poder rellenarse desde el portal

**Dónde:** `apps/superadmin-web/src/App.tsx`.

El API ya lo acepta (`CreateTenantRequest.ContactEmail`) y la entidad lo persiste,
pero el formulario tiene `contactEmail` en el estado y **ningún input que lo rellene**.
Falta la mitad del arreglo.

### 5. Los endpoints de sincronización no están en la interfaz

**Dónde:** `GET/POST /api/superadmin/tenants/core-sync`.

El informe de tenants desincronizados y la reparación bajo demanda funcionan, pero
hay que invocarlos con curl. Deberían tener una vista en el portal: es ahí donde
alguien notaría un tenant sin enlazar o con puntero colgante.

El informe ya demostró su valor: detectó un puntero roto en producción al primer uso.

### 6. Revisar si el formulario de edición envía los campos comerciales

**Dónde:** `apps/superadmin-web/src/TenantEdit.tsx`.

`UpdateTenantRequest` acepta ahora los campos espejo de Core con semántica
"nulo = no tocar", y `UpdateTenant` los propaga. Falta comprobar si la pantalla de
edición los expone; si no, se sigue propagando solo el subconjunto antiguo.

---

## Deuda técnica

### 7. Patrón outbox para el dual-write

Excluido del alcance a propósito, por volumen.

Hoy el alta es todo-o-nada: si Core falla, se revierte y se devuelve 502. Correcto,
pero **sin reintento**: con Core caído no se pueden dar altas y hay que repetirlas a
mano. Un outbox en `FelDb` escrito en la misma transacción, drenado por `Fel.Worker`,
daría entrega al-menos-una-vez y reparación automática.

Requiere la idempotencia que ya existe (adopción por `platform_id, country_id, slug`),
porque al-menos-una-vez implica reintentos duplicados.

### 8. Sin tests automáticos

Todo se verificó a mano contra base y Core reales. La lógica que más los merece:
adopción de ficha existente, reversión ante fallo de Core, y borrado compensatorio
cuando Core confirma pero el commit local falla. Son caminos difíciles de reproducir
a mano y fáciles de romper sin darse cuenta.

---

## Facturación electrónica

### 12. Reconstruir documentos que existen en Dataico pero no en `FelDb`

**Por qué hace falta:** hoy, crear una Nota Crédito/Débito exige que el documento
original ya sea un `Document` propio con `DataicoDocumentId` (ver
`DataicoSubmissionProvider.SubmitAsync`, que busca el original con
`_dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)`). Si un cliente
ya facturaba directo en Dataico antes de usar Facil Factura, esos documentos no
existen en nuestra base y no se les puede hacer ningún ajuste (NC sobre factura, ND
sobre NC, etc.) — bloquea la cadena completa de ajustes sobre historial preexistente.

**Diseño acordado (pendiente de construir):** una carga inicial por resolución desde
el portal del Tenant — el usuario define el prefijo y el número inicial de una
resolución, y el sistema itera `GET /direct/dataico_api/v2/invoices?number=N`,
incrementando N, reconstruyendo cada `Document`+`DocumentItem` a partir de la
respuesta, hasta que deje de encontrar documentos (se define un umbral de fallos
consecutivos para detener la iteración, no el primer fallo — la numeración DIAN
permite huecos por anulaciones).

**Bloqueante real:** `GET /direct/dataico_api/v2/invoices?number=...` existe en el
Postman de Dataico pero **nunca se usó en el proyecto legado de referencia
(`C:\FEL`)** — no hay ningún ejemplo de respuesta guardado, así que no se sabe con
certeza si trae los ítems, en qué formato, ni qué devuelve para un número que no
existe (para saber cuándo detener la iteración). Antes de programar el
reconstructor hace falta que alguien con credenciales reales de Dataico haga esa
consulta contra una factura conocida y comparta la respuesta completa.

Una vez confirmada la respuesta, falta además decidir: ritmo de llamadas (evitar
rate-limit de Dataico en una carga con muchos documentos), y si el mismo mecanismo
aplica a Documento Soporte/Nómina o solo a Factura/NC/ND por ahora.

---

## Nuevas líneas de negocio

### 13. Integración con IHCE (historia clínica electrónica interoperable)

**Por qué hace falta:** un cliente pidió un servicio de integración de su historia
clínica hacia la plataforma nacional. Investigado a fondo (solo investigación, nada
construido todavía) el 2026-09-12.

**Qué es:** IHCE (Interoperabilidad de Historia Clínica Electrónica), operada por
MinSalud, obligatoria desde el 15 de abril de 2026 para todo prestador registrado en
REPS (Resolución 1888 de 2025, reglamenta la Ley 2015 de 2020). El mecanismo es
generar y transmitir un **RDA** (Resumen Digital de Atención en Salud) — un Bundle
**HL7 FHIR R4** — hacia la plataforma nacional. Hay 4 tipos de RDA: Paciente, Consulta
Externa, Hospitalización, Urgencias, con ~30 perfiles FHIR entre los cuatro.

**Es un sistema distinto al que ya integramos.** El MUV-FEV-RIPS
(`src/Fel.Api.Tenant/Services/MinSalud/MinSaludMuvService.cs`) es para *facturación*
de servicios de salud y usa `LoginSISPRO`. IHCE es la plataforma *clínica*, con su
propio estándar (FHIR) y autenticación (API Key ClientID/ClientSecret emitida por
Hércules SISPRO). No hay reuso técnico directo entre ambas integraciones.

**Punto de arquitectura clave:** las credenciales (ClientID/ClientSecret) pertenecen
al prestador/IPS, no al proveedor de software — mismo patrón que el certificado
digital DIAN de cada `Client` (`NitValidation`/`ICryptoVault`): FacilFactura sería el
operador tecnológico que genera y transmite el RDA, pero con la identidad del
cliente, nunca la propia.

**Fases propuestas y tiempos estimados** (total ~10-20 semanas para el primer tipo de
RDA en producción; solo las fases 0-2 están bajo nuestro control):

0. Investigación y documentación — bajar y estudiar la Guía de Implementación FHIR
   oficial (`vulcano.ihcecol.gov.co`) y el Anexo Técnico No.1 — 1-2 semanas.
1. Modelo de datos + mapeo mínimo — mapear el modelo clínico del cliente a los
   perfiles FHIR de un solo tipo de RDA, empezando por Consulta Externa (el más
   simple) — 2-4 semanas.
2. Generación del Bundle FHIR + transporte — generador del Bundle FHIR R4/JSON +
   cliente HTTP con API Key hacia el gateway de IHCE — 3-5 semanas.
3. Registro Hércules + pruebas + certificación — el cliente designa un "Delegado
   Administrativo" y se registra en Hércules → sandbox hasta HTTP 200 → el
   Ministerio valida conformidad. **Depende del cliente y de MinSalud, no de
   nosotros** — 4-10 semanas.
4. Piloto en producción — activar transmisión real + integrar el Visor RDA +
   monitoreo — 1-2 semanas + operación continua.

**Bloqueante real antes de comprometer alcance:** no se pudo leer el contenido del
**Anexo Técnico No.1** (endpoints REST exactos, formatos de error) — el PDF de
Hércules está compuesto casi enteramente de imágenes. Hay que solicitarlo a
`soporte_ihce@minsalud.gov.co` o conseguirlo en un formato con texto extraíble antes
de dimensionar la Fase 2 con precisión.

**Preguntas que solo el cliente puede responder:** en qué formato tiene hoy su
historia clínica (HL7 v2, CDA, base de datos propia — define el esfuerzo real de
mapeo), si ya tiene el registro Hércules/Delegado Administrativo hecho, y si buscan
que seamos el sistema clínico completo o solo el conector hacia IHCE sobre lo que ya
tienen.

**Certificación:** directamente ante el Ministerio de Salud y Protección Social, vía
la plataforma Hércules del SISPRO (`hercules.sispro.gov.co`) — no hay tercero
delegado.

---

## Despliegue

### 9. El script despliega el árbol de trabajo entero

**Dónde:** `scripts/deploy.ps1`.

`tar` empaqueta todo el directorio, así que no hay forma de desplegar un cambio sin
arrastrar lo que haya sin commitear. En esta sesión subieron ~40 rutas de trabajo
previo junto con el trabajo del día.

Alternativa: desplegar desde un checkout limpio de una referencia de git en vez de
desde el directorio local.

### 10. `fel-migrator` es punto único de fallo, por diseño

Los cinco servicios .NET dependen de él con `service_completed_successfully`: si no
sale en 0, **ninguno arranca**. Es intencionado —preferible a arrancar con el esquema
desalineado— pero conviene que esté escrito en el runbook, porque el síntoma es una
caída total y la causa no es obvia.

Diagnóstico: `docker inspect fel-migrator --format '{{.State.ExitCode}}'` y
`docker logs fel-migrator`.

### 11. `api.facil-factura.pro` responde 404 en la raíz

Cosmético, pero el paso de verificación del deploy lo muestra junto a los 200 de los
demás sitios, como si fuera un fallo. Un endpoint de salud en la raíz lo resolvería y
serviría de comprobación real.

---

## Notas de contexto

- **Core no necesita despliegue** para nada de lo anterior: todas las operaciones usan
  columnas y endpoints de PostgREST que ya existen. El proyecto CLI está en
  `C:\Desarrollos\supabase\Core` y se aplica con `supabase db push`, no con EF.
- **Las migraciones de `FelDb`** las aplica `fel-migrator`, no el arranque de las APIs.
  En desarrollo local `Fel.Api.Tenant` sigue migrando al arrancar, tras `IsDevelopment()`.
- **nginx resuelve los upstreams en tiempo de ejecución** (`resolver` + variable). No
  volver a nombres literales en `proxy_pass`: al recrearse los contenedores, Docker
  rota las IPs y el tráfico acaba en el servicio equivocado.
