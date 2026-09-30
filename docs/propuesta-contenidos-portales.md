# Propuesta de contenidos para los portales

## Alcance

Revisión de textos visibles hardcodeados en:

- `apps/superadmin-web` (portal admin).
- `apps/tenant-web` (portal tenant).
- `apps/client-web` (portal cliente).

Se excluyeron textos internos de código, nombres de variables, rutas, nombres de API, clases CSS y contenido generado por la API. Se mantienen los términos fiscales y regulatorios que ayudan a completar el formulario; se simplifican las explicaciones de implementación, REPX, DevExpress, XAdES, HMAC y detalles del motor.

> Las líneas son orientativas y pueden cambiar si se edita el archivo. Los textos repetidos de autenticación se documentan una sola vez indicando todos los archivos.

## Criterios de redacción

- Usar lenguaje directo y orientado a la tarea.
- Mostrar una explicación solo cuando evita un error o una duda real.
- Dejar los detalles técnicos en ayuda contextual o documentación para desarrolladores.
- Usar siempre “cliente” en la interfaz; reservar `Client`, `Tenant`, `REPX` y “Motor DevExpress” para identificadores técnicos inevitables.
- Preferir “documentos” sobre abreviaturas como “Docs.” cuando haya espacio.

## Textos compartidos de acceso

Archivos: `apps/superadmin-web/src/ForgotPassword.tsx`, `apps/tenant-web/src/pages/ForgotPassword.tsx`, `apps/client-web/src/pages/ForgotPassword.tsx`.

| Texto original | Nueva propuesta |
|---|---|
| `Recuperar Contraseña` | `Recuperar contraseña` |
| `Te enviaremos un enlace para restablecerla.` | `Te enviaremos un enlace para crear una nueva contraseña.` |

Archivos: `apps/superadmin-web/src/ResetPassword.tsx`, `apps/tenant-web/src/pages/ResetPassword.tsx`, `apps/client-web/src/pages/ResetPassword.tsx`.

| Texto original | Nueva propuesta |
|---|---|
| `Crear Nueva Contraseña` | `Crear nueva contraseña` |
| `Correo electrónico maestro` | `Correo electrónico` |
| `Contraseña de alta seguridad` | `Contraseña` |
| `Cerrar Sesión` | `Cerrar sesión` |
| `Colapsar menú` | `Contraer menú` |
| `Expandir menú` | `Expandir menú` |

## Portal admin (`superadmin-web`)

### `apps/superadmin-web/src/App.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Conectando al Motor Central...` | `Cargando...` |
| `Panel de Control` | `Resumen` |
| `Tenants Activos` | `Clientes activos` |
| `Docs Procesados` | `Documentos procesados` |
| `Proyección Mes` | `Proyección del mes` |
| `Gestión de Tenants` | `Gestión de cuentas` |
| `Registrar Tenant` | `Registrar cuenta` |
| `Leyendo el RUT...` | `Leyendo el RUT...` |
| `Información Principal` | `Información básica` |
| `Razón Social (Legal Name)` | `Razón social` |
| `Nombre Comercial Secundario` | `Nombre comercial alternativo` |
| `Slug (Identificador URL)` | `Identificador de acceso` |
| `Información Fiscal (DIAN)` | `Información fiscal` |
| `NIT / ID Fiscal` | `NIT` |
| `Email Facturación Electrónica` | `Correo de facturación electrónica` |
| `Configuración Regional` | `Configuración regional` |
| `Filtra las direcciones y sugiere idioma, moneda y zona horaria.` | `Define el país, idioma, moneda y zona horaria.` |
| `Dirección Física` | `Dirección` |
| `Buscar Dirección` | `Buscar dirección` |
| `Dirección (Línea 1)` | `Dirección` |
| `Dirección (Línea 2, Opcional)` | `Complemento de dirección (opcional)` |
| `Estado/Departamento` | `Departamento` |
| `Vista previa del mapa` | `Mapa` |
| `Selecciona una dirección para ver el mapa` | `Selecciona una dirección para verla en el mapa` |
| `Email de Facturación (cuenta)` | `Correo de la cuenta` |
| `Cuenta de Administrador` | `Administrador de la cuenta` |
| `Le enviamos una invitación a este correo para que el tenant establezca su propia contraseña e inicie sesión en su portal (tenants.facil-factura.pro).` | `Enviaremos una invitación para crear la contraseña y entrar al portal.` |
| `Email (Login)` | `Correo de acceso` |
| `No hay tenants registrados en el sistema.` | `Aún no hay cuentas registradas.` |
| `Superadmin` | `Administrador` |
| `Configuración del Motor` | `Configuración` |
| `No se pudieron cargar los países disponibles.` | `No se pudieron cargar los países.` |
| `Tenant registrado con éxito.` | `Cuenta registrada correctamente.` |
| `Error al crear Tenant` | `No se pudo registrar la cuenta` |

### `apps/superadmin-web/src/Billing.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Análisis de Facturación - Plataforma` | `Resumen de facturación` |
| `Desglose en tiempo real de consumo por Tenant (Mes Actual)` | `Consumo por cuenta durante el mes actual` |
| `Total Documentos Emitidos` | `Documentos emitidos` |
| `Cuentas por Cobrar Estimadas` | `Cuentas por cobrar` |
| `Volumen Docs` | `Documentos` |
| `Tarifa de Nivel Aplicada` | `Tarifa aplicada` |
| `Subtotal Adeudado` | `Subtotal` |

### `apps/superadmin-web/src/Certificates.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Certificados Digitales - Todos los Tenants` | `Certificados digitales` |
| `Vista consolidada de los certificados de firma electrónica de todos los clientes, en todos los tenants, ordenados por fecha de vencimiento - para saber qué hay que provisionar.` | `Consulta el estado y vencimiento de los certificados de tus cuentas.` |
| `Cargando...` | `Cargando certificados...` |

### `apps/superadmin-web/src/DocumentTemplates.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Sube archivos .repx (DevExpress Report Designer) y gestiona sus versiones para este tipo de documento.` | `Administra las plantillas y sus versiones para este tipo de documento.` |
| `Nombre del Modelo` | `Nombre de la plantilla` |
| `Nueva Plantilla Base` | `Nueva plantilla` |
| `Archivo .repx` | `Archivo de plantilla` |
| `El archivo se sube directo a Facil Reports; la plantilla queda en Borrador hasta que la publiques.` | `La plantilla quedará como borrador hasta que la publiques.` |
| `Subir Nueva Versión` | `Subir nueva versión` |
| `Al guardar, se creará un borrador de la versión siguiente. Tu plantilla actualmente publicada no será afectada hasta que publiques la nueva versión.` | `Se creará un borrador. La versión publicada seguirá activa hasta que publiques la nueva.` |
| `Publicado` / `Borrador` / `Archivado` | `Publicada` / `Borrador` / `Archivada` |

### `apps/superadmin-web/src/DocumentTypes.tsx`, `IdentificationTypes.tsx`, `UnitOfMeasureCatalog.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `DocumentTypes.tsx` | `Gestiona los tipos de documentos DIAN permitidos en el sistema.` | `Administra los tipos de documento disponibles.` |
| `DocumentTypes.tsx` | `Código (Ej. FE, NC, DS)` | `Código (ej. FE, NC, DS)` |
| `DocumentTypes.tsx` | `Nombre Descriptivo` | `Nombre del documento` |
| `IdentificationTypes.tsx` | `Equivalencia Dataico (opcional)` | `Código del proveedor (opcional)` |
| `IdentificationTypes.tsx` | `Código que espera la API de Dataico para este tipo de identificación. Si se deja vacío, se usa el valor de respaldo fijo del sistema.` | `Código usado por el proveedor, si aplica.` |
| `UnitOfMeasureCatalog.tsx` | `Sigla UN/CEFACT` | `Sigla de unidad` |
| `UnitOfMeasureCatalog.tsx` | `Va tal cual en el XML UBL (unitCode). Para "Unidad" es el histórico "94"; el resto suele coincidir con la sigla.` | `Código de unidad usado en la factura.` |
| `UnitOfMeasureCatalog.tsx` | `Un Client puede forzar el mismo formato para todas sus unidades desde su propia configuración.` | `El cliente puede definir este formato para todas sus unidades.` |

### `apps/superadmin-web/src/Integrators.tsx`, `RetentionEngine.tsx`, `TaxCatalog.tsx`, `TariffTiers.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `Integrators.tsx` | `Código (único, sin espacios)` | `Código` |
| `Integrators.tsx` | `El código no se puede cambiar una vez creado.` | `El código no se puede cambiar después de crear el integrador.` |
| `Integrators.tsx` | `Directo DIAN (el Client factura con su propio SoftwareId)` | `Directo a la DIAN` |
| `Integrators.tsx` | `Integrador Externo (tramita en nombre del Client, ej. Dataico)` | `Proveedor externo` |
| `RetentionEngine.tsx` | `GroupKey (código interno)` | `Código interno` |
| `RetentionEngine.tsx` | `Rótulo genérico (selector del producto)` | `Nombre visible` |
| `RetentionEngine.tsx` | `Categoría (Dataico)` | `Categoría` |
| `RetentionEngine.tsx` | `Se calcula sobre` | `Base de cálculo` |
| `RetentionEngine.tsx` | `Ej. cuando el gobierno actualiza el UVT: agrega una fila nueva con el valor y la fecha desde la que aplica, en vez de editar la anterior - así las facturas ya emitidas siguen calculando con el valor que tenían vigente.` | `Crea una nueva vigencia cuando cambie el valor. Así se conservan los cálculos de documentos anteriores.` |
| `TaxCatalog.tsx` | `Tipo de cuenta bancaria` | `Cuenta bancaria` |
| `TaxCatalog.tsx` | `La tarifa concreta de esta combinación categoría+tarifa (ej. RET_ICA al 0.966%).` | `Define la tarifa para esta categoría.` |
| `TariffTiers.tsx` | `Integrador (vacío = tier global)` | `Integrador (opcional)` |
| `TariffTiers.tsx` | `Global (todos los integradores sin tier propio)` | `Global` |
| `TariffTiers.tsx` | `Hasta (vacío = sin tope)` | `Hasta (opcional)` |

### `apps/superadmin-web/src/TenantEdit.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Edición Fiscal y Facturación` | `Datos fiscales y facturación` |
| `Perfil Fiscal (DIAN)` | `Información fiscal` |
| `Nombre Comercial (visible en portal de tenant y cliente)` | `Nombre comercial` |
| `Grupo empresarial (tenant padre)` | `Grupo empresarial` |
| `Si este tenant factura a través de otro (ej. DGS le paga a R&W, y R&W nos paga a nosotros), asigna aquí ese tenant padre. El padre podrá ver el consolidado de lo emitido a todo el grupo.` | `Si pertenece a un grupo, selecciona la cuenta principal para consolidar la facturación.` |
| `Ubicación (Google Maps)` | `Ubicación` |
| `Información Comercial y de Contacto` | `Datos de contacto` |
| `Regionalización` | `Preferencias regionales` |
| `Moneda por Defecto (Id de Core)` | `Moneda` |
| `Administradores del Tenant` | `Administradores de la cuenta` |
| `Tarifario Transaccional (Pricing)` | `Tarifas por documento` |
| `Define el costo unitario de cada documento (en COP).` | `Define el valor por documento en COP.` |
| `Modo de Facturación (Superadmin → Tenant)` | `Modo de cobro` |
| `Por defecto se cobra por documento (tarifario de arriba). Úsalo Por Usuario solo para tenants de marca blanca que no facturan con nuestro motor DIAN.` | `Elige si el cobro se calcula por documento o por usuario.` |
| `Cada Client (emisor) activo del tenant cuenta como un usuario.` | `Cada emisor activo cuenta como un usuario.` |
| `Integradores Habilitados` | `Integradores disponibles` |
| `Tarifa por Integrador` | `Tarifa del integrador` |
| `Usando default del Tenant` | `Usando tarifa de la cuenta` |
| `Le enviaremos un correo de invitación para que establezca su propia contraseña.` | `Enviaremos una invitación para crear la contraseña.` |

## Portal tenant (`tenant-web`)

### `apps/tenant-web/src/App.tsx`, `src/pages/Dashboard.tsx`, `src/pages/GroupBilling.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `App.tsx` | `Facturación Propia (Opcional)` | `Facturación propia` |
| `Dashboard.tsx` | `Dashboard Analítico` | `Resumen` |
| `Dashboard.tsx` | `Monitorea el estado de tus comprobantes electrónicos y cortes de facturación en tiempo real.` | `Consulta tus documentos y el estado de tu facturación.` |
| `Dashboard.tsx` | `Resumen Financiero (Mes Actual)` | `Resumen financiero del mes` |
| `Dashboard.tsx` | `Ingresos Esperados (Cuentas por Cobrar)` | `Ingresos esperados` |
| `Dashboard.tsx` | `Suma del consumo de tus clientes basado en sus tarifas individuales.` | `Total según el consumo de tus clientes.` |
| `Dashboard.tsx` | `Deuda a Plataforma (Cuentas por Pagar)` | `Pago a la plataforma` |
| `Dashboard.tsx` | `Desglose de Consumo por Cliente` | `Consumo por cliente` |
| `Dashboard.tsx` | `Docs. Emitidos` | `Documentos emitidos` |
| `Dashboard.tsx` | `Tarifa Configurada` | `Tarifa` |
| `Dashboard.tsx` | `Subtotal Adeudado` | `Subtotal` |
| `Dashboard.tsx` | `Últimos Documentos Enviados` | `Últimos documentos enviados` |
| `GroupBilling.tsx` | `Facturación de mi grupo` | `Facturación del grupo` |
| `GroupBilling.tsx` | `Este tenant no tiene otros tenants asociados todavía.` | `Aún no hay otras cuentas asociadas.` |
| `GroupBilling.tsx` | `Cuando otro tenant se registre bajo tu grupo empresarial, aquí verás el consolidado de lo que se les ha emitido.` | `Cuando haya cuentas asociadas, aquí verás su facturación consolidada.` |

### `apps/tenant-web/src/pages/Clients.tsx`, `Associates.tsx`, `Developers.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `Clients.tsx` | `Gestión de Clientes (Emisores)` | `Clientes emisores` |
| `Clients.tsx` | `Administra los negocios que emitirán facturas bajo tu cuenta.` | `Administra los negocios que emiten documentos desde tu cuenta.` |
| `Clients.tsx` | `Nuevo Cliente` | `Nuevo cliente` |
| `Clients.tsx` | `Correo Recepción` | `Correo de recepción` |
| `Clients.tsx` | `Próx. Vencimiento Resolución` | `Vencimiento de resolución` |
| `Clients.tsx` | `No hay clientes registrados aún.` | `Aún no hay clientes registrados.` |
| `Associates.tsx` | `Tus comerciales - asígnales los clientes que gestionan cada uno.` | `Asigna clientes a las personas de tu equipo.` |
| `Associates.tsx` | `Nuevo Asociado` | `Nuevo miembro del equipo` |
| `Developers.tsx` | `Developers` | `Desarrolladores` |
| `Developers.tsx` | `Invitar Developer` | `Invitar desarrollador` |
| `Developers.tsx` | `Aún no has invitado a ningún developer.` | `Aún no has invitado a ningún desarrollador.` |
| `Developers.tsx` | `Le enviaremos un enlace para que establezca su propia contraseña en el portal de developers.` | `Enviaremos un enlace para crear la contraseña y entrar al portal de desarrolladores.` |

### `apps/tenant-web/src/components/ClientFormFields.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Cargar desde RUT (PDF)` | `Cargar datos desde el RUT` |
| `Llena identidad, contacto y ubicación por ti - revísalos antes de guardar.` | `Completa algunos datos automáticamente. Revísalos antes de guardar.` |
| `Identidad Tributaria` | `Información tributaria` |
| `Régimen Fiscal` | `Régimen tributario` |
| `Responsabilidades adicionales (RUT casilla 53)` | `Responsabilidades tributarias` |
| `Asociado (comercial a cargo)` | `Responsable comercial` |
| `Contacto y Ubicación (Google Maps)` | `Contacto y ubicación` |
| `Correo de Alertas / Facturación` | `Correo de notificaciones` |
| `Código DANE` | `Código DANE del municipio` |
| `El código DANE es obligatorio para emitir directo a la DIAN (sin Dataico) - cárgalo desde el RUT o escríbelo a mano.` | `Este código es necesario para emitir directamente a la DIAN.` |
| `Tarifa a este Cliente` | `Tarifa para este cliente` |
| `Lo que le cobras a este cliente por el servicio - independiente de cómo Facil Factura te cobra a ti.` | `Valor que cobras a este cliente por el servicio.` |

### `apps/tenant-web/src/pages/Branding.tsx`, `Certificates.tsx`, `DocumentTemplates.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `Branding.tsx` | `Personaliza los colores y el logotipo para la vista de tus clientes.` | `Personaliza el logo y los colores que verán tus clientes.` |
| `Branding.tsx` | `Identificador del Micrositio (Slug)` | `Identificador del sitio` |
| `Branding.tsx` | `Este será el enlace público donde tus clientes ingresarán. Solo usa minúsculas y guiones.` | `Será el enlace público para tus clientes. Usa minúsculas y guiones.` |
| `Branding.tsx` | `Este color se usa en el portal de tus clientes (clients.facil-factura.pro), no en este panel.` | `Este color se usará en el portal de tus clientes.` |
| `Branding.tsx` | `Controla si tus clientes pueden ver su información de consumo/facturación en su propio portal.` | `Define si tus clientes pueden consultar su consumo y facturación.` |
| `Branding.tsx` | `Usar SSL/TLS (recomendado)` | `Usar conexión segura (SSL/TLS)` |
| `Branding.tsx` | `No tienes bolsas prepago compradas todavía.` | `Aún no tienes paquetes prepago.` |
| `Certificates.tsx` | `Certificados Digitales (.p12)` | `Certificados digitales` |
| `Certificates.tsx` | `Vista consolidada de los certificados de firma electrónica (XAdES-EPES) de todos tus clientes, ordenados por fecha de vencimiento.` | `Consulta los certificados de tus clientes y sus fechas de vencimiento.` |
| `DocumentTemplates.tsx` | `Visualiza los diseños globales o clónalos para personalizarlos para tus clientes.` | `Elige un diseño o crea una copia para personalizarla.` |
| `DocumentTemplates.tsx` | `Llave REPX (Motor DevExpress)` | `Identificador de plantilla` |
| `DocumentTemplates.tsx` | `Se creará un borrador de la siguiente versión. Tu plantilla publicada actual no se ve afectada hasta que publiques la nueva versión.` | `Se creará un borrador; la versión publicada seguirá activa hasta que publiques la nueva.` |

### `apps/tenant-web/src/pages/Resolutions.tsx`, `ClientEdit.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `Resolutions.tsx` | `Mis Resoluciones DIAN` | `Resoluciones DIAN` |
| `Resolutions.tsx` | `Aún no facturas desde la plataforma` | `Aún no has emitido documentos desde la plataforma.` |
| `ClientEdit.tsx` | `Certificado Digital (Firma Electrónica)` | `Certificado digital` |
| `ClientEdit.tsx` | `Carga el certificado .p12 o .pfx para firmar XML en nombre de este emisor.` | `Carga el certificado digital del emisor.` |
| `ClientEdit.tsx` | `Credenciales de Integración B2B` | `Credenciales de integración` |
| `ClientEdit.tsx` | `Credenciales de Producción (Live)` | `Credenciales de producción` |
| `ClientEdit.tsx` | `Usa estas credenciales para emitir documentos con validez legal.` | `Usa estas credenciales para emitir documentos en producción.` |
| `ClientEdit.tsx` | `Credenciales de Pruebas (Test / Sandbox)` | `Credenciales de pruebas` |
| `ClientEdit.tsx` | `Entorno de habilitación de la DIAN.` | `Entorno de pruebas de la DIAN.` |
| `ClientEdit.tsx` | `Qué puede emitir este Client desde su formulario de facturación.` | `Documentos que puede emitir este cliente.` |
| `ClientEdit.tsx` | `Qué conceptos de retención puede elegir este Client por línea de factura.` | `Retenciones disponibles para este cliente.` |
| `ClientEdit.tsx` | `Eventos RADIAN se disparan solos al recibir un documento. Reclamo implica una disputa formal - solo actívalo si el cliente tiene una validación confiable antes.` | `Selecciona los eventos que se crearán automáticamente al recibir un documento.` |
| `ClientEdit.tsx` | `Automatiza la habilitación pegando el enlace de acceso enviado por la DIAN al correo del cliente.` | `Completa la habilitación con el enlace enviado por la DIAN.` |
| `ClientEdit.tsx` | `Este cliente ha completado satisfactoriamente los requisitos técnicos y puede emitir comprobantes con validez legal.` | `Este cliente está habilitado para emitir documentos en producción.` |

## Portal cliente (`client-web`)

### `apps/client-web/src/App.tsx`, `src/pages/Login.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `App.tsx` | `Portal de Facturación` | `Facturación` |
| `App.tsx` | `Bienvenido a tu Portal de Facturación` | `Bienvenido a tu portal de facturación` |
| `App.tsx` | `Resumen de tu actividad en el mes actual.` | `Resumen de tu actividad este mes.` |
| `App.tsx` | `Total Documentos (Mes)` | `Documentos este mes` |
| `App.tsx` | `Cuentas por Pagar (Servicio FEL)` | `Saldo por pagar` |
| `App.tsx` | `Módulo en construcción...` | `Esta sección estará disponible próximamente.` |
| `Login.tsx` | `Correo de acceso` | `Correo electrónico` |
| `Login.tsx` | `Contraseña` | `Contraseña` |

### `apps/client-web/src/pages/CustomersPage.tsx`, `ProductsPage.tsx`, `PayrollPage.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `CustomersPage.tsx` | `Mis Terceros` | `Terceros` |
| `CustomersPage.tsx` | `Clientes, proveedores y empleados para tus documentos electrónicos` | `Administra clientes, proveedores y empleados.` |
| `ProductsPage.tsx` | `Mis Productos` | `Productos y servicios` |
| `ProductsPage.tsx` | `Catálogo de bienes y servicios` | `Catálogo de productos y servicios` |
| `ProductsPage.tsx` | `Nombre / Descripción` | `Nombre o descripción` |
| `ProductsPage.tsx` | `Precio Base (Sin impuestos)` | `Precio base` |
| `ProductsPage.tsx` | `Superadmin no ha configurado impuestos adicionales en el catálogo todavía.` | `No hay impuestos adicionales configurados.` |
| `PayrollPage.tsx` | `Nómina Electrónica` | `Nómina electrónica` |
| `PayrollPage.tsx` | `Historial de comprobantes de nómina emitidos` | `Historial de nómina emitida` |
| `PayrollPage.tsx` | `No tienes empleados registrados. Créalos primero en "Mis Terceros".` | `No tienes empleados registrados. Créales un registro en Terceros.` |
| `PayrollPage.tsx` | `Sin devengos.` / `Sin deducciones.` | `No hay devengos.` / `No hay deducciones.` |

### `apps/client-web/src/pages/InvoicesPage.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Ingresa los datos para emitir un nuevo documento` | `Completa los datos del documento` |
| `Resolución de Facturación` | `Resolución` |
| `No hay resoluciones activas registradas. Configúralas en Ajustes → Resoluciones.` | `No hay resoluciones activas. Configúralas en Ajustes > Resoluciones.` |
| `Cliente / Adquirente` | `Cliente` |
| `Fecha de Factura` | `Fecha de emisión` |
| `Tipo de Pago` | `Condiciones de pago` |
| `Líneas de Factura` | `Productos o servicios` |
| `Precio Und.` | `Precio unitario` |
| `Desc. %` | `Descuento %` |
| `Retenciones Generales (opcional)` | `Retenciones (opcional)` |
| `Sin retenciones generales (ej. ReteICA, ReteIVA) en este documento.` | `No hay retenciones agregadas.` |
| `Superadmin no ha configurado retenciones en el catálogo todavía.` | `No hay retenciones configuradas.` |
| `Descuento General (opcional)` | `Descuento general (opcional)` |
| `Cargos Generales (opcional)` | `Cargo general (opcional)` |
| `Observaciones (opcional)` | `Observaciones (opcional)` |
| `Nuevo Producto` | `Nuevo producto` |
| `Tratamiento de IVA` | `Tratamiento del IVA` |
| `No hay facturas que coincidan con el filtro.` | `No hay documentos que coincidan con el filtro.` |
| `Total del periodo:` | `Total del período:` |

### `apps/client-web/src/pages/ReceivedDocumentsPage.tsx`, `SupportDocumentsPage.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `ReceivedDocumentsPage.tsx` | `Eventos de Recepción` | `Documentos recibidos` |
| `ReceivedDocumentsPage.tsx` | `Facturas y notas de tus proveedores, y los eventos RADIAN que le informas a la DIAN sobre ellas.` | `Consulta documentos de tus proveedores y gestiona sus eventos.` |
| `ReceivedDocumentsPage.tsx` | `Cuáles eventos se disparan solos al recibir un documento (por correo o carga manual). Los que no actives quedan disponibles para disparar a mano.` | `Elige qué eventos se crearán automáticamente al recibir documentos.` |
| `ReceivedDocumentsPage.tsx` | `Aún no has recibido ni cargado ningún documento.` | `Aún no tienes documentos recibidos.` |
| `SupportDocumentsPage.tsx` | `Compras a proveedores no obligados a facturar` | `Compras a proveedores no obligados a facturar electrónicamente` |
| `SupportDocumentsPage.tsx` | `Generando Nota de Ajuste` | `Generando nota de ajuste` |
| `SupportDocumentsPage.tsx` | `Esta nota ajustará el documento soporte seleccionado.` | `La nota ajustará el documento seleccionado.` |
| `SupportDocumentsPage.tsx` | `Sin retenciones generales (ej. ReteICA, ReteIVA) en este documento.` | `No hay retenciones agregadas.` |
| `SupportDocumentsPage.tsx` | `Superadmin no ha configurado retenciones en el catálogo todavía.` | `No hay retenciones configuradas.` |

### `apps/client-web/src/pages/ResolutionsSettings.tsx`

| Texto original | Nueva propuesta |
|---|---|
| `Mis Resoluciones DIAN` | `Resoluciones DIAN` |
| `Gestiona tus autorizaciones de numeración para emitir facturas electrónicas válidas ante la DIAN.` | `Administra las autorizaciones de numeración para emitir facturas.` |
| `Tu empresa ya está sincronizada y lista para emitir facturación electrónica real ante la DIAN.` | `Tu empresa está lista para emitir facturas en producción.` |
| `Automatiza tu proceso de habilitación pegando el enlace que te envió la DIAN. Nosotros hacemos el resto.` | `Pega el enlace de habilitación enviado por la DIAN y completa el proceso.` |
| `Has completado satisfactoriamente los requisitos técnicos de la DIAN.` | `Has completado los requisitos de la DIAN.` |
| `Aún no tienes resoluciones` | `Aún no tienes resoluciones` |
| `Carga tu primer formulario 1876 de la DIAN para empezar a facturar legalmente.` | `Carga tu formulario 1876 para comenzar a facturar.` |
| `Extracción Inteligente de PDF` | `Cargar datos desde PDF` |
| `Sube el Formulario 1876 y llenaremos todo mágicamente.` | `Sube el formulario 1876 y completaremos los datos disponibles.` |

### `apps/client-web/src/pages/TemplateSettings.tsx`, `TemplateEditor.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `TemplateSettings.tsx` | `Logo para tus Comprobantes` | `Logo de tus documentos` |
| `TemplateSettings.tsx` | `Unidad de Medida en tus Facturas` | `Unidad de medida en tus facturas` |
| `TemplateSettings.tsx` | `Correo para Reenvío de Documentos` | `Correo para reenvío de documentos` |
| `Usar SSL/TLS (recomendado)` | `Usar conexión segura (SSL/TLS)` |
| `Mis Diseños Propios (todos los estados)` | `Mis diseños` |
| `Selecciona un Comprobante` | `Selecciona un documento` |
| `Selecciona un tipo de comprobante en la lista de la izquierda para ver los diseños disponibles.` | `Selecciona un documento para ver sus diseños.` |
| `Clonar y Personalizar` | `Copiar y personalizar` |
| `Llave REPX (Motor DevExpress)` | `Identificador de plantilla` |
| `Nueva Llave REPX (Motor DevExpress)` | `Nuevo identificador de plantilla` |
| `Tu plantilla publicada actual no se ve afectada hasta que publiques esta nueva versión.` | `La versión publicada seguirá activa hasta que publiques la nueva.` |
| `Falta el identificador de la plantilla.` | `Falta el identificador de la plantilla.` |

### `apps/client-web/src/components/CustomerFormModal.tsx`, `ImportExcelButton.tsx`

| Archivo | Texto original | Nueva propuesta |
|---|---|---|
| `CustomerFormModal.tsx` | `Cargar desde RUT (PDF)` | `Cargar datos desde el RUT` |
| `CustomerFormModal.tsx` | `Sirve para persona natural o jurídica - llena los campos por ti, revísalos antes de guardar.` | `Completa algunos datos automáticamente. Revísalos antes de guardar.` |
| `CustomerFormModal.tsx` | `Dirección (Google Maps)` | `Dirección` |
| `CustomerFormModal.tsx` | `Ciudad (auto-completado por Google)` | `Ciudad` |
| `CustomerFormModal.tsx` | `Código DANE (Depto+Municipio)` | `Código DANE` |
| `CustomerFormModal.tsx` | `Datos de Nómina Electrónica` | `Datos de nómina` |
| `ImportExcelButton.tsx` | `Resultado de la Importación` | `Resultado de la importación` |

## Propuesta de dashboard para el portal cliente

### Situación actual

Archivo principal: `apps/client-web/src/App.tsx`.

Actualmente el dashboard muestra principalmente el total de documentos del mes y un valor por pagar. Son datos insuficientes para que el cliente sepa qué debe hacer, si sus documentos están siendo aceptados y si tiene alguna configuración pendiente.

### Objetivo

Convertir el inicio en un resumen operativo: permitir que el usuario entienda en pocos segundos cómo va su facturación y pueda continuar directamente con la siguiente tarea.

### Propuesta de estructura

| Zona | Contenido propuesto | Texto sugerido |
|---|---|---|
| Encabezado | Saludo y período consultado | `Resumen de tu facturación` / `Consulta la actividad de tu empresa durante este mes.` |
| Acción principal | Acceso directo para emitir | `Emitir documento` |
| Acción secundaria | Accesos frecuentes | `Nuevo cliente`, `Nuevo producto`, `Ver documentos` |
| Tarjeta 1 | Documentos emitidos en el mes | `Documentos emitidos` + total |
| Tarjeta 2 | Documentos aceptados por la DIAN | `Aceptados` + total |
| Tarjeta 3 | Documentos pendientes o rechazados | `Requieren atención` + total |
| Tarjeta 4 | Valor facturado en el período | `Total facturado` + valor |
| Estado de configuración | Validación de requisitos básicos | `Tu cuenta está lista para facturar` o `Completa estos pasos para comenzar` |
| Actividad reciente | Últimos documentos emitidos | `Actividad reciente` |
| Ayuda contextual | Qué hacer ante pendientes | `Revisa los documentos que requieren atención.` |

### Contenido recomendado

#### 1. Resumen de emisión

Mostrar cuatro indicadores simples y accionables:

- Total de documentos emitidos durante el período.
- Documentos aceptados por la DIAN.
- Documentos pendientes de validación.
- Documentos rechazados o con error.

Cada tarjeta debería enlazar a la lista de documentos con el filtro correspondiente. No se recomienda mostrar identificadores técnicos, respuestas XML ni mensajes extensos en esta zona.

#### 2. Estado de la cuenta

Mostrar solo si existe una tarea pendiente. Por ejemplo:

| Situación | Texto propuesto | Acción |
|---|---|---|
| No hay resolución activa | `Configura una resolución para empezar a facturar.` | `Configurar resolución` |
| Falta certificado | `Carga el certificado digital de tu empresa.` | `Configurar certificado` |
| Falta información básica | `Completa los datos de tu empresa.` | `Completar información` |
| Todo listo | `Tu cuenta está lista para facturar.` | `Emitir documento` |

El bloque debe desaparecer cuando no haya pendientes, para que el dashboard no se sienta lleno de información repetida.

#### 3. Actividad reciente

Mostrar entre cinco y diez documentos con:

- Fecha.
- Tipo de documento.
- Número.
- Cliente o adquirente.
- Total.
- Estado en lenguaje claro.
- Acción contextual, por ejemplo `Ver`, `Descargar` o `Reenviar`.

Estados sugeridos para el usuario:

| Estado técnico | Texto visible |
|---|---|
| `Accepted` | `Aceptado` |
| `Pending` | `En validación` |
| `Rejected` | `Rechazado` |
| `Draft` | `Borrador` |
| Error de envío | `No enviado` |

#### 4. Acciones rápidas

Las acciones más frecuentes deberían estar visibles sin entrar al menú:

- `Emitir factura`.
- `Emitir nota crédito`.
- `Crear documento soporte`.
- `Agregar cliente`.
- `Agregar producto`.
- `Ver todos los documentos`.

Si el usuario no tiene habilitado algún tipo de documento, la acción debe ocultarse o mostrar una explicación breve, por ejemplo: `Este documento aún no está habilitado para tu cuenta.`

#### 5. Consumo y pagos

Si el servicio permite consultar consumo o cobros, mostrarlo como una tarjeta secundaria, no como el elemento principal del dashboard:

- Documentos incluidos o consumidos en el período.
- Saldo o valor pendiente.
- Fecha del próximo cobro, si aplica.
- Enlace `Ver facturación`.

Texto sugerido: `Consumo del período` y `Valor pendiente`. Evitar `Cuentas por Pagar (Servicio FEL)`, porque es técnico y poco natural para el cliente final.

### Wireframe funcional

```text
┌─────────────────────────────────────────────────────────────┐
│ Resumen de tu facturación                 [Emitir documento] │
│ Consulta la actividad de tu empresa este mes.                │
├──────────────┬──────────────┬──────────────┬───────────────┤
│ Emitidos     │ Aceptados    │ En validación│ Requieren     │
│ 128          │ 121          │ 4            │ atención: 3   │
├──────────────────────────────────┬──────────────────────────┤
│ Actividad reciente               │ Acciones rápidas         │
│ Fecha · Documento · Estado       │ [Emitir factura]          │
│ ...                              │ [Agregar cliente]        │
│ [Ver todos los documentos]       │ [Agregar producto]       │
├──────────────────────────────────┴──────────────────────────┤
│ Estado de la cuenta: Tu cuenta está lista para facturar.     │
└─────────────────────────────────────────────────────────────┘
```

### Datos que debería consumir el dashboard

Se recomienda que el backend entregue un resumen agregado, en lugar de que el frontend descargue todos los documentos para calcularlo:

- `periodStart` y `periodEnd`.
- `totalIssued`.
- `totalAccepted`.
- `totalPending`.
- `totalRejected`.
- `totalBilled`.
- `recentDocuments`.
- `pendingSetupItems`.
- `amountDue` y `nextBillingDate`, si están disponibles.

Una posible ruta sería `GET /client/dashboard/summary`. El nombre puede ajustarse a las convenciones actuales de la API.

### Prioridad de implementación

1. Reemplazar las dos tarjetas actuales por los cuatro estados de documentos.
2. Agregar actividad reciente con enlace al detalle.
3. Agregar acciones rápidas para emitir documentos y crear datos maestros.
4. Agregar alertas de configuración pendientes.
5. Incorporar consumo y pagos como información secundaria.

## Recomendaciones de implementación

1. Centralizar los textos compartidos de autenticación, estados y mensajes vacíos en un archivo de traducciones o constantes por portal.
2. Revisar que todos los textos nuevos estén guardados en UTF-8; durante la inspección varios resultados de consola se mostraron con codificación incorrecta (`Ã³`, `â€”`), aunque el contenido fuente parece estar en español.
3. Sustituir primero las referencias visibles a `Tenant`, `Client`, `Superadmin`, `Developer`, `REPX`, `DevExpress`, `XAdES-EPES`, `HMAC` y dominios internos. Esos valores pueden permanecer en documentación técnica o en una ayuda avanzada.
4. Mantener sin simplificar los nombres regulatorios que el usuario debe reconocer: DIAN, RUT, NIT, DV, CIIU, RADIAN, RIPS y Formulario 1876.

