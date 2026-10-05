# Plan de adecuación responsive del portal de clientes

## Objetivo

Dejar `apps/client-web` usable y consistente en teléfonos, tabletas, portátiles y monitores grandes, sin páginas, modales, tablas, formularios, filtros ni estados de operación pendientes de adaptar.

El alcance incluye todas las rutas actuales del portal, sus componentes compartidos, los formularios de creación/edición, diálogos de confirmación, modales de detalle y carga de archivos, y el editor de plantillas. No se considera terminado por “verse bien” únicamente en escritorio: debe validarse el flujo completo en móvil con teclado virtual, scroll, orientación vertical y horizontal, y contenido largo.

## Inventario de superficies

| Área | Rutas o componentes | Tratamiento requerido |
|---|---|---|
| Acceso | `/login`, `/forgot-password`, `/reset-password` | Tarjeta fluida, campos y mensajes legibles, teclado y viewport móvil |
| Shell | `App.tsx`, sidebar, header, dashboard | Navegación móvil, header compacto, contenido sin overflow horizontal |
| Inicio | Dashboard | Tarjetas, actividad reciente, acciones rápidas y estados vacíos |
| Documentos | `/invoices`, `/support-documents`, `DocumentsPage` | Formulario extenso, tablas, filtros, detalle, respuesta DIAN, reenvío y producto rápido |
| Terceros | `/customers`, `CustomerFormModal` | Tabla/listado, búsqueda, formulario, mapa y acciones |
| Productos | `/products`, `ImportExcelButton` | Tabla, formulario, importación y resultados de carga |
| Nómina | `/payroll` | Listado, creación, detalle, conceptos, paginación y exportación |
| Recibidos | `/received-documents` | Configuración, prueba de conexión, tabla y estados |
| Resoluciones | `/resolutions` | Habilitación, tablas, carga, prueba DIAN y modal de creación |
| Plantillas | `/settings`, `TemplateSettings` | Configuración, tarjetas/preview, SMTP, leyendas y modales |
| Diseñador | `/template-editor`, `TemplateEditor` | Envolvente responsive; el canvas DevExpress conserva una superficie mínima de escritorio con scroll controlado |
| Compartidos | `DecimalInput`, toaster, confirm dialog, botones, campos | Tamaño táctil, foco, mensajes, wrapping y consistencia |

## Hallazgos iniciales

La revisión del código identifica estos riesgos concretos que el trabajo debe resolver:

- El `Layout` usa sidebar permanente, header de altura fija y padding horizontal amplio; en móvil debe existir un modo de navegación compacto sin consumir el ancho de la página.
- El dashboard incluye una tabla de actividad y tarjetas que necesitan versión compacta para anchos pequeños.
- `DocumentsPage` concentra el mayor riesgo: formulario de más de una pantalla, varias tablas de ítems/impuestos/retenciones, filtros con controles de ancho fijo, tablas de listado y varios modales.
- Los listados de terceros, productos, nómina, recibidos y resoluciones son tablas completas. El scroll horizontal puede ser un mecanismo de respaldo, pero no debe ser la única experiencia móvil para información y acciones esenciales.
- Hay rejillas `grid-cols-2`, `grid-cols-3` y `grid-cols-5` que deben pasar a una columna o a una composición progresiva en móvil.
- Existen varios modales con `max-h` y padding propios (`80vh`, `90vh`, etc.). Deben compartir una regla de viewport seguro, scroll interno y footer visible.
- Hay textos largos como nombres de terceros, descripciones, CUFE, XML y mensajes DIAN. Deben romper línea sin deformar tarjetas ni generar overflow de la página.
- El editor DevExpress no es un formulario móvil convencional. Se debe adaptar su shell y declarar una experiencia de canvas con zoom/scroll, evitando intentar comprimir el diseñador hasta hacerlo inutilizable.

## Estándar responsive obligatorio

### Breakpoints y estructura

- Móvil pequeño: 320–374 px.
- Móvil estándar: 375–639 px.
- Tableta: 640–1023 px.
- Escritorio: 1024 px en adelante.
- Validar también 1280, 1440 y 1920 px para evitar que el contenido quede excesivamente estirado.
- El contenido principal tendrá ancho fluido, `min-width: 0` en flex/grid y un ancho máximo razonable cuando aplique.
- Nunca debe existir scroll horizontal en `body`, shell o tarjetas. El scroll horizontal solo se permitirá dentro de tablas o del canvas del diseñador cuando sea una decisión explícita.

### Navegación y shell

- Sidebar colapsado en móvil mediante drawer, con botón accesible y cierre al navegar.
- Header con logo/nombre truncado de forma segura, acciones visibles y sin empujar el contenido.
- Área principal con padding progresivo, por ejemplo `p-4` en móvil, `sm:p-6`, `lg:p-8`.
- Altura basada en viewport dinámico (`dvh`) para no ocultar contenido detrás de barras del navegador.
- Focus visible, navegación por teclado y etiquetas/aria-label para controles de icono.

### Controles y formularios

- Campos a una columna en móvil; dos o más columnas solo desde el breakpoint que permita leer etiquetas y valores sin compresión.
- Botones de acción agrupados con `flex-wrap`, ancho completo cuando corresponda y orden lógico en móvil.
- Área inferior de formularios con acciones accesibles después de recorrer el contenido; no se debe depender de botones fuera del viewport.
- Objetivo táctil mínimo de 44 × 44 px para botones, cierres, paginación, checkboxes y acciones de tabla, aplicado en móvil/tableta (por debajo de `md` o con `pointer: coarse`); en escritorio se conserva la densidad actual.
- Fechas, selectores, búsqueda y ordenamiento deben poder usarse con teclado móvil. El cambio de una fecha no debe disparar una reorganización que haga perder foco o cierre el control.
- Errores de validación deben aparecer junto al campo y también en un resumen accesible cuando el formulario sea largo.

### Tablas y listados

Se distinguen dos categorías, porque tienen costo y riesgo muy distintos:

- **Listados de consulta** (documentos, terceros, productos, nómina, documentos recibidos, resoluciones): en móvil se convierten en tarjetas responsive. Se implementan con un componente común `ResponsiveList` (tabla desde `md`, tarjetas por debajo) para no repetir el patrón en cada pantalla.
- **Tablas de captura** (ítems, impuestos, retenciones, devengos/deducciones): en móvil pasan a bloques editables (un bloque por fila, con edición inline o en modal), no a tarjetas de solo lectura. Es la parte más costosa y de mayor riesgo; se resuelve junto con el formulario que las contiene.

- No se aceptará una tabla de escritorio comprimida ni el scroll horizontal como experiencia principal. Cada tarjeta debe organizar la información por jerarquía: identificación/estado, datos principales, valores y acciones.
- Las tarjetas deben mantener una apariencia de producto terminado: espaciado consistente, agrupación visual, badges de estado, acciones táctiles y posibilidad de expandir información secundaria sin saturar la pantalla.
- En tabletas anchas y escritorio se podrá conservar la tabla si resulta adecuada. El componente debe cambiar explícitamente entre presentación de tabla y presentación de tarjetas según breakpoint.
- El scroll interno queda reservado para contenido técnico o excepcionalmente ancho, como XML, CUFE/QR en texto, logs y el canvas del diseñador; no será la solución para listados de negocio.
- No se deben cortar nombres, estados, totales ni acciones. Los textos largos deben usar wrapping/break-word.
- Las acciones de fila deben permanecer accesibles sin exigir precisión de puntero.
- Totales y paginación deben pasar a bloques apilados en móvil.
- Las columnas secundarias pueden ocultarse solo si la información sigue disponible en detalle.

### Modales, drawers y estados

- Patrón común: overlay con `p-2`/`p-4`, panel `w-full`, `max-h-[calc(100dvh-2rem)]`, `overflow-hidden`, cuerpo con scroll y footer que no desaparece.
- En móvil los formularios largos pueden usar panel casi pantalla completa o bottom sheet; no deben quedar centrados con contenido inaccesible.
- Cierre por botón, Escape cuando corresponda y clic fuera solo cuando no haya riesgo de pérdida de datos.
- Bloquear el scroll del documento mientras el modal esté abierto y devolver el foco al control que lo abrió.
- Toaster y mensajes de error no deben tapar botones ni quedar fuera de pantalla.
- Loading, vacío, error, sin resultados, éxito y progreso de importación deben tener versiones compactas y legibles.

## Plan de ejecución por fases

### Fase 0 — Línea base y contrato visual

1. Crear tokens/utilidades compartidas para spacing responsive, paneles, campos, botones, tablas, modal, focus y overflow.
2. Identificar y reemplazar reglas duplicadas de modales.
3. Crear un componente base `Modal` sobre `@radix-ui/react-dialog` (ya se usa `@radix-ui/react-alert-dialog`), que entregue gratis foco, Escape, bloqueo de scroll y retorno de foco, con el patrón `dvh` + footer visible.
4. Crear el componente `ResponsiveList` (tabla desde `md`, tarjetas por debajo) para los listados de consulta.
5. Añadir una prueba automática de overflow horizontal con Playwright: por cada ruta y viewport de la matriz, verifica que `document.documentElement.scrollWidth <= clientWidth`. Hoy el proyecto no tiene framework de pruebas de navegador (solo `oxlint` y `tsc -b && vite build`).
6. Añadir una guía de componentes o fixtures internos con: campo normal/error, botón, tabla, modal corto/largo, texto largo, loading y empty state.
7. Definir una regla de lint/revisión para detectar `min-w` o anchos fijos nuevos sin justificación.

**Salida:** contrato visual responsive aprobado, componentes base reutilizables (`Modal`, `ResponsiveList`) y prueba automática de overflow funcionando.

### Fase 1 — Shell, autenticación e inicio

- Adaptar `Sidebar`, `Layout`, header, rutas y dashboard.
- Implementar drawer móvil y conservar la preferencia de sidebar únicamente para escritorio.
- Rehacer tarjetas de resumen con una composición que funcione en 320 px.
- Convertir actividad reciente en tarjetas compactas o tabla desplazable controlada.
- Revisar logo, nombre, NIT, botones de acción, estados de configuración y consumo.
- Validar login, recuperación y restablecimiento con teclado virtual y mensajes largos.

**Criterio:** desde 320 px no aparece overflow de página y todas las acciones principales están disponibles sin zoom.

### Fase 2 — Terceros, productos e importación

- Adaptar `CustomersPage`, `CustomerFormModal`, `ProductsPage` e `ImportExcelButton`.
- Convertir los listados en tarjetas en móvil usando `ResponsiveList`.
- Pasar formularios de dos columnas a una columna y revisar mapa, identificaciones, tipos de tercero, retenciones y campos condicionales.
- Garantizar que resultados de importación, errores por fila y botones de descarga no excedan el panel.

**Criterio:** crear, editar, buscar, eliminar e importar se puede completar en móvil, incluyendo errores y confirmaciones.

### Fase 3 — Facturas y documentos de soporte

- Trabajar sobre el componente compartido `DocumentsPage`, manteniendo separadas las reglas de negocio de factura y soporte.
- Adaptar encabezado, selector de fechas, filtros, búsqueda, paginación y exportación.
- Reorganizar el formulario: tercero/resolución/forma de pago/plazo/orden de compra, observaciones, ítems, IVA, retenciones, leyenda y totales.
- En ítems, usar una fila compacta con edición por bloques o modal en móvil; no forzar todas las columnas en pantalla.
- Aplicar la misma solución a impuestos y retenciones, con valores y porcentajes legibles.
- Adaptar resumen, detalle, preview PDF, respuesta DIAN, reenvío, producto rápido y confirmación de eliminación/publicación.

> Nota: el caso sin retenciones (totales y CUFE/QR montados en la representación gráfica) es un tema de la plantilla REPX, no de responsive; se gestiona como tarea aparte.

**Criterio:** crear/editar/publicar/ver/reenviar/eliminar un documento de ambos tipos sin scroll global, pérdida de foco o acciones ocultas.

### Fase 4 — Nómina y documentos recibidos

- Adaptar los modos de creación, edición, detalle y listado de `PayrollPage`.
- Convertir tablas de devengos/deducciones y el listado principal a bloques compactos en móvil.
- Mantener visibles estado, consecutivo, empleado, periodo, neto y acciones.
- Adaptar filtros de fechas, búsqueda, exportación, paginación y acciones de publicación/eliminación.
- Adaptar `ReceivedDocumentsPage`, sus formularios de conexión, prueba de conexión, tabla y mensajes de integración.

**Criterio:** los flujos de nómina y recibidos son operables en móvil y los estados técnicos no generan overflow por XML/mensajes.

### Fase 5 — Resoluciones, ajustes y plantillas

- Adaptar `ResolutionsSettings`: tarjetas de estado, carga de formularios, tablas de resoluciones, pruebas DIAN y modal de creación.
- Adaptar `TemplateSettings`: tarjetas/preview, SMTP, leyendas, configuraciones y modales de confirmación.
- En tablas de resoluciones y plantillas, preservar prefijo, rango, estado y acciones en móvil.
- En `TemplateEditor`, adaptar barra superior, botón volver, título largo y contenedor; mantener el diseñador en su superficie mínima con scroll/zoom interno controlado y mensaje claro si el viewport no permite edición cómoda.

**Criterio:** configuración y cambio de estado funcionan en móvil; el diseñador no rompe el shell ni la navegación y no produce scroll horizontal del portal.

### Fase 6 — Modales y componentes transversales

Auditar individualmente todos los overlays y migrarlos al componente base `Modal`. La lista se deriva del código (`grep "fixed inset-0"`), no de memoria; a la fecha hay 10 overlays:

- `CustomerFormModal` (1).
- `ImportExcelButton` (1).
- `DocumentsPage` (3): incluye producto rápido, respuesta DIAN y reenvío; confirmar cuál es el tercero.
- `ProductsPage` (1).
- `ResolutionsSettings` (2): progreso/prueba y creación.
- `TemplateSettings` (2): configuración y SMTP.
- Confirmaciones globales (`ConfirmDialog`, en `@shared`) y toasts.

Al cerrar la fase se repite el `grep` para confirmar que no quedan overlays fuera del patrón.

Cada modal debe tener: tamaño seguro en `dvh`, scroll interno, footer visible, cierre accesible, focus management, wrapping, estado de carga y comportamiento al rotar el dispositivo.

### Fase 7 — Verificación y cierre

1. Revisar que no queden rutas sin inventariar ni componentes con comportamiento solo de escritorio.
2. Ejecutar build y lint de `client-web`.
3. Hacer prueba manual por flujo y viewport.
4. Ejecutar una prueba de overflow con contenido artificialmente largo: nombre de empresa, tercero, producto, observación, correo, CUFE, mensaje DIAN y XML.
5. Registrar capturas antes/después solo para casos donde el cambio sea difícil de verificar por código.
6. Cerrar la tarea con la matriz completa en verde; las únicas excepciones admitidas son las declaradas explícitamente (canvas del diseñador DevExpress) y quedan documentadas.

## Matriz mínima de pruebas

| Viewport | Propósito |
|---|---|
| 320 × 568 | móvil pequeño y contenido mínimo |
| 375 × 667 | móvil estándar |
| 390 × 844 | móvil moderno con viewport alto |
| 667 × 375 | móvil horizontal |
| 768 × 1024 | tableta vertical |
| 1024 × 768 | tableta horizontal / breakpoint escritorio |
| 1280 × 800 | portátil |
| 1440 × 900 | escritorio estándar |
| 1920 × 1080 | escritorio amplio y límites de ancho |

En cada tamaño se deben probar: iniciar sesión, navegar, crear/editar documento, usar fecha personalizada, abrir/cerrar cada modal, validar errores, cargar archivo, ver detalle, publicar, descargar/abrir PDF/XML, paginar, buscar, filtrar y cerrar sesión.

## Definición de terminado

- Todas las rutas y componentes del inventario tienen una decisión responsive implementada y verificada.
- Cero overflow horizontal involuntario en el portal.
- Ningún modal deja controles o contenido esencial fuera del viewport.
- Todos los formularios se pueden completar con teclado móvil y muestran errores sin romper el layout.
- Todas las tablas tienen estrategia móvil explícita y acciones utilizables.
- Los textos largos no deforman tarjetas, filas, botones ni diálogos.
- El comportamiento responsive es consistente entre factura, documento de soporte, nómina, terceros, productos, recibidos, resoluciones y plantillas.
- Build y lint pasan.
- La matriz de pruebas está documentada, sin pendientes conocidos ni excepciones silenciosas (las excepciones explícitas, como el canvas del diseñador, quedan escritas).
- La prueba automática de overflow pasa en todas las rutas y viewports.

## Orden recomendado de implementación

`Fase 0 → Fase 1 → Fase 6 → Fase 2 → Fase 3 → Fase 4 → Fase 5 → Fase 7`.

La razón es estabilizar primero el shell y los patrones de modal, ya que todas las pantallas dependen de ellos; después se puede adaptar el formulario compartido de documentos y reutilizar las mismas reglas en nómina y configuración.

## Backlog ejecutable

### Bloque A — Base responsive

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-001 | Crear tokens y utilidades responsive para padding, gaps, paneles, campos, botones, tablas y modales. | P0 | — | Los patrones base se pueden reutilizar sin duplicar clases especiales. |
| RESP-002 | Crear el componente común `Modal` sobre `@radix-ui/react-dialog` con `dvh`, scroll interno, footer visible y cierre accesible. | P0 | RESP-001 | Todos los modales pueden adoptar el mismo contrato visual. |
| RESP-038 | Crear `ResponsiveList` (tabla desde `md`, tarjetas por debajo) para los listados de consulta. | P0 | RESP-001 | Terceros, productos, documentos, nómina, recibidos y resoluciones lo pueden reutilizar sin duplicar el patrón. |
| RESP-039 | Configurar Playwright con la prueba automática de overflow horizontal por ruta y viewport. | P0 | — | La prueba corre en local y detecta un overflow introducido a propósito. |
| RESP-003 | Revisar `body`, `#root`, `Layout` y contenedores flex para eliminar overflow horizontal global. | P0 | RESP-001 | La página no desborda en 320 px con contenido normal y largo. |
| RESP-004 | Definir tamaños táctiles, focus visible, estados disabled/loading y reglas de wrapping. | P0 | RESP-001 | Los controles principales cumplen el estándar táctil y de teclado. |

### Bloque B — Shell y acceso

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-005 | Implementar sidebar como drawer en móvil y conservar colapsado solo para escritorio. | P0 | RESP-001 | Se puede navegar y cerrar el menú desde 320 px. |
| RESP-006 | Adaptar header, logo, nombre, NIT, avatar y acciones para anchos pequeños. | P0 | RESP-001 | El header no corta contenido ni desplaza la página. |
| RESP-007 | Adaptar dashboard: encabezado, botón principal, tarjetas, actividad reciente y acciones rápidas. | P1 | RESP-003, RESP-005 | Dashboard usable en todos los viewports de la matriz. |
| RESP-008 | Adaptar login, recuperación y restablecimiento de contraseña. | P1 | RESP-001 | Campos, errores y botones funcionan con teclado virtual. |

### Bloque C — Modales compartidos

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-009 | Migrar `CustomerFormModal` al patrón común y reorganizar su formulario a una columna en móvil. | P0 | RESP-002 | El formulario completo se puede recorrer y guardar sin perder el footer. |
| RESP-010 | Migrar `ImportExcelButton` y sus resultados al patrón común. | P1 | RESP-002 | Errores por fila, progreso y descargas son legibles en móvil. |
| RESP-011 | Adaptar confirmaciones globales y toasts. | P1 | RESP-002, RESP-004 | No tapan botones ni salen del viewport. |
| RESP-012 | Migrar todos los overlays (lista derivada con `grep "fixed inset-0"`, incluido `ProductsPage`) al componente `Modal`. | P1 | RESP-002 | Cada modal devuelve el foco, se cierra de forma consistente y el `grep` no encuentra overlays fuera del patrón. |

### Bloque D — Terceros y productos

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-013 | Adaptar listado de terceros: búsqueda, tarjetas, acciones, estados vacíos y paginación. | P1 | RESP-038 | En móvil cada tercero se muestra como tarjeta responsive; la tabla queda para anchos mayores. |
| RESP-014 | Revisar formulario de terceros: tipos, identificación, contacto, mapa y campos condicionales. | P1 | RESP-009 | Crear y editar funciona en 320 px y tableta. |
| RESP-015 | Adaptar listado de productos y filtros. | P1 | RESP-001 | En móvil cada producto se muestra como tarjeta; nombre, código, scope, IVA/retenciones y acciones siguen accesibles. |
| RESP-016 | Adaptar formulario de producto y validaciones responsive. | P1 | RESP-001 | No hay rejillas comprimidas ni botones fuera de pantalla. |

### Bloque E — Facturas y documentos de soporte

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-017 | Adaptar encabezado, filtros de fecha, búsqueda, exportación y paginación de `DocumentsPage`. | P0 | RESP-001 | Cambiar fechas, filtrar y paginar no rompe el layout ni el foco; el listado móvil usa tarjetas. |
| RESP-018 | Adaptar bloque de tercero, resolución, forma de pago, plazo, orden de compra y observaciones. | P0 | RESP-001 | Campos obligatorios y condicionales son usables en móvil. |
| RESP-019 | Adaptar tabla/formulario de ítems para móvil, incluyendo producto rápido. | P0 | RESP-002 | Agregar, editar y eliminar ítems funciona mediante tarjetas o bloques editables, sin tabla ilegible. |
| RESP-020 | Adaptar impuestos, retenciones y leyendas. | P0 | RESP-001 | Porcentajes, bases, valores y acciones se leen sin overflow. |
| RESP-021 | Adaptar bloque de descuentos, cargos y totales. | P0 | RESP-001 | Los totales permanecen visibles y ordenados en una columna. |
| RESP-022 | Adaptar acciones de guardar, editar, publicar, eliminar y cancelar. | P0 | RESP-004 | Las acciones permanecen disponibles y no se superponen. |
| RESP-023 | Adaptar detalle del documento: metadatos, ítems, impuestos, retenciones, QR, CUFE y totales. | P1 | RESP-001 | Los textos largos se rompen y el detalle es navegable. |
| RESP-024 | Adaptar modal de respuesta DIAN y modal de reenvío. | P1 | RESP-002 | XML, CUFE, errores y botones se visualizan en móvil. |
| RESP-025 | Verificar paridad responsive entre factura y documento de soporte. | P0 | RESP-017 a RESP-024 | Ambos modos tienen el mismo estándar sin mezclar reglas de negocio. |

### Bloque F — Nómina y documentos recibidos

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-026 | Adaptar listado de nómina, filtros, fechas, búsqueda, exportación y paginación. | P1 | RESP-001 | El listado móvil usa tarjetas y sus acciones funcionan sin tabla comprimida. |
| RESP-027 | Adaptar formulario de nómina y conceptos devengados/deducciones. | P1 | RESP-001 | Las listas de conceptos no generan columnas ilegibles. |
| RESP-028 | Adaptar detalle de nómina, publicación, eliminación y documentos relacionados. | P1 | RESP-002 | El detalle completo se puede revisar y operar en móvil. |
| RESP-029 | Adaptar documentos recibidos, configuración, prueba de conexión y listado. | P1 | RESP-001 | El listado móvil usa tarjetas; estados técnicos, mensajes y acciones son legibles. |

### Bloque G — Resoluciones y plantillas

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-030 | Adaptar tarjetas de estado, carga y prueba DIAN de resoluciones. | P1 | RESP-001 | Progreso, errores y resultados funcionan en móvil. |
| RESP-031 | Adaptar listado en tarjetas y modal de resoluciones. | P1 | RESP-002 | En móvil prefijo, rango, fechas, estado y acciones se muestran como tarjeta accesible. |
| RESP-032 | Adaptar ajustes de plantillas, previews, SMTP y leyendas. | P1 | RESP-001, RESP-002 | Formularios y modales no exceden el viewport. |
| RESP-033 | Adaptar shell del `TemplateEditor` y documentar el comportamiento mínimo del canvas. | P2 | RESP-001 | Header y navegación son responsive; el canvas usa scroll/zoom interno controlado. |

### Bloque H — Calidad y cierre

| ID | Tarea | Prioridad | Dependencia | Terminado cuando |
|---|---|---:|---|---|
| RESP-034 | Crear checklist de pruebas por ruta, modal y flujo. | P0 | RESP-005 a RESP-033 | Ninguna superficie queda sin caso de prueba. |
| RESP-035 | Ejecutar pruebas en 320, 375, 390, 667 horizontal, 768, 1024, 1280, 1440 y 1920 px (automática de overflow + manual de flujos críticos). | P0 | RESP-034, RESP-039 | La matriz queda documentada sin fallos conocidos. |
| RESP-036 | Probar contenido extremo: CUFE, XML, nombres, descripciones, correos, observaciones y errores DIAN largos. | P0 | RESP-034 | No hay deformaciones ni overflow involuntario. |
| RESP-037 | Ejecutar lint y build de `client-web`, corregir regresiones y revisar rutas completas. | P0 | Todas las tareas anteriores | Build/lint pasan y no quedan pendientes responsive. |

## Decisiones tomadas

1. **Uso en móvil:** aún no se conoce la proporción real de usuarios en teléfono, y el equipo también lo usa desde el teléfono. Se deja todo listo: no se baja la prioridad de ninguna fase. Facturar (crear/publicar documento y consultar el listado) es flujo crítico en móvil.
2. **Ancho máximo del contenido:** `max-w-screen-2xl` (1536 px), centrado. Por debajo es fluido (hay clientes con pantallas de ~1200 px y el equipo usa resoluciones amplias).
3. **Tabla vs. tarjetas:** `ResponsiveList` cambia a tarjetas por debajo de `lg` (1024 px) para listados con muchas columnas (documentos, nómina); los de pocas columnas (terceros) pueden cambiar en `md`. El sidebar arranca colapsado entre 1024 y 1279 px para que a ~1200 px el contenido tenga ~1120 px.
4. **Pruebas manuales por entrega:** login, crear y publicar un documento, y crear un tercero. El resto lo cubre la prueba automática de overflow. La verificación visual en distintas resoluciones la hace el equipo y devuelve retroalimentación a cada entrega.

## Entregas sugeridas

- **Entrega 1:** RESP-001 a RESP-012, RESP-038 y RESP-039 — base, shell, modales, `ResponsiveList` y prueba de overflow.
- **Entrega 2:** RESP-013 a RESP-025 — terceros, productos, facturas y documentos de soporte.
- **Entrega 3:** RESP-026 a RESP-033 — nómina, recibidos, resoluciones y plantillas.
- **Entrega 4:** RESP-034 a RESP-037 — pruebas, correcciones y cierre.

## Estado de implementación (4 de octubre de 2026)

Todas las superficies del inventario tienen una decisión responsive implementada y verificada con la prueba automática. **Pendiente: la verificación manual en dispositivos reales**, que hace el equipo.

### Qué se hizo

| Bloque | Resultado |
|---|---|
| A. Base | `Modal` (sobre Radix Dialog / shadcn), `ResponsiveList`, `RowIconButton`, `Button` de shadcn; `ConfirmDialog` migrado a shadcn `AlertDialog`. Toasts siguen con `sonner`. |
| B. Shell y acceso | Menú como drawer bajo 1024 px; columna fija desde `lg`, colapsada por defecto entre 1024 y 1279 px. Header con logo (ancho máximo) y nombre + NIT en dos líneas. Dashboard con actividad reciente en filas. Login, recuperar y restablecer con `dvh` y `autocomplete`. |
| C. Modales | Todos los modales usan `Modal` salvo la pantalla de progreso de la habilitación DIAN (`ResolutionsSettings`), que es bloqueante a propósito y no debe tener botón de cerrar. |
| D–G. Pantallas | Terceros, productos, facturas, documentos soporte, nómina, recibidos, resoluciones, plantillas y editor adaptados. En el formulario de documentos, los ítems son una tarjeta editable por línea bajo `lg` y tabla desde `lg`. |
| H. Calidad | Prueba automática de overflow (`npm run e2e`), 176 casos en los 9 viewports de la matriz, todos en verde. |

### Decisiones de diseño que no estaban en el plan original

- **Modal con selectores flotantes (`withFloatingPickers`).** `SearchableSelect` y el autocompletado de Google Maps pintan su lista en un portal sobre `<body>`; un Dialog modal de Radix les quita los clics y les roba el foco. Esos modales usan el modo no modal de Radix con overlay propio y no se cierran con clic fuera ni Escape.
- **Umbrales de tabla por listado.** Con el menú expandido (256 px) y el padding de la página, una tabla ancha no cabe en 1280 px aunque ya sea "escritorio". `ResponsiveList` acepta `tableFrom` = `md` | `lg` | `xl` | `wide` (1400 px) | `2xl` | `ultra` (1800 px). Por debajo se muestran tarjetas (dos columnas desde `sm`). Asignación actual: terceros y productos `wide`; facturas y documentos soporte `wide`; nómina y recibidos `2xl`; resoluciones `ultra`.
- **Columnas secundarias ocultas en pantallas medianas** (`hideBelow`): subtotal, impuestos y retenciones del listado de facturas, fecha de la nómina y origen de recibidos solo se ven en la tabla desde 1800 px; siempre están en la tarjeta y en el detalle.
- **Filtros del listado de documentos**: de dos en dos en móvil, una fila con salto desde `sm`.
- **Editor de plantillas**: el lienzo de DevExpress conserva una superficie mínima de escritorio (1024 × 600) y se desplaza dentro de su propio contenedor; bajo `lg` muestra un aviso.

### Defectos encontrados y corregidos en el camino

- `ConceptTable` en nómina era un componente definido dentro del render: cada tecla lo remontaba y el campo perdía el foco. Ahora es una función de render. Mismo criterio en los campos de ítem de documentos.
- El estado vacío de plantillas usaba `col-span-2` en una rejilla de una columna (columna implícita que ensanchaba la página); y la rejilla de plantillas no tenía `min-w-0`, así que un nombre largo ensanchaba toda la pantalla.
- `ResponsiveList` aplicaba las clases de celda también al encabezado, y la celda de acciones no era una fila flex (los botones se apilaban).
- El diálogo de confirmación no tenía margen lateral a 320 px.

### Cómo correr la prueba automática

`npm run e2e` desde `apps/client-web`. Construye la app, la sirve con `vite preview` y simula la API interceptando `/api` con datos deliberadamente largos (`e2e/mockApi.ts`). Mide que ni la página ni el contenedor del shell tengan scroll horizontal, que los modales queden dentro de la pantalla y que el drawer funcione. `e2e/run.mjs` copia `apps/_shared` dentro de la app durante la prueba (igual que el Dockerfile) y la borra al terminar; no debe quedar una copia dentro de `apps/client-web`, porque el despliegue empaqueta el árbol completo.

### Checklist de prueba manual por flujo (RESP-034)

Probar en un teléfono (≈390 px), una tableta (768 px) y escritorio (1200 y 1440+ px):

1. Iniciar sesión, abrir y cerrar el menú, navegar por todas las secciones y cerrar sesión.
2. Terceros: crear (natural y jurídico), editar, buscar, eliminar, importar Excel (con filas con error).
3. Productos: crear y editar con "Otros impuestos", elegir unidad de medida e IVA con los selectores.
4. Factura: crear con varios ítems (producto del catálogo y libre), descuento, retenciones por línea y generales, guardar borrador, editar, emitir, ver detalle, ver respuesta de la DIAN, reenviar, imprimir.
5. Documento soporte: igual que factura, sin IVA; nota de ajuste.
6. Nómina: crear con devengos y deducciones, emitir, nota de reemplazo y de eliminación.
7. Documentos recibidos: guardar configuración, probar conexión, cargar ZIP/XML, disparar eventos.
8. Resoluciones: crear (con PDF y a mano), editar el próximo consecutivo, leyenda por prefijo, consecutivos de notas.
9. Ajustes: logo, formatos, SMTP, clonar plantilla, nueva versión, publicar.
10. Con un nombre de empresa, un tercero y un correo muy largos: que nada se deforme.
11. Girar el teléfono con un modal abierto: que no queden botones fuera de pantalla.
