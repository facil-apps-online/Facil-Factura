Documento de Especificación de Arquitectura y Diseño Técnico

Proyecto: Plataforma Multi-Tenant de Centralización y Abstracción de Servicios DIAN

Modalidad Legal: Operación como Software Propio (Sin Intermediación de Proveedor Tecnológico)Entorno de Despliegue: Servidor Linux Dedicado (On-Premise / VPS)1. Objetivo del Sistema y AlcanceEl objetivo principal del sistema es actuar como un vehículo tecnológico centralizado que abstrae la complejidad técnica de los Web Services de la DIAN para empresas que desean operar bajo la figura de Software Propio.La plataforma transforma peticiones JSON sencillas y rápidas enviadas por los clientes en documentos XML estructurados (UBL 2.1), firmados digitalmente de forma local y transmitidos de manera asíncrona mediante protocolos SOAP corporativos.2. Modelo de Actores y Niveles de Acceso (Multi-Tenancy)



El sistema implementa una arquitectura web de aislamiento de datos en tres niveles de consumo:



Nivel 1: Superadministrador (Ustedes):Gestión, aprovisionamiento y suspensión de Tenants.

Métricas globales de salud del servidor (uso de CPU/Memoria, tamaño de colas de mensajería).

Módulo de facturación y cobro a Tenants basado en el volumen de peticiones API procesadas exitosamente.

Consola de monitoreo de disponibilidad de los endpoints de la DIAN.

Nivel 2: Tenants (Distribuidores / Marca Blanca):Administración y creación de sus propios Clientes Finales.Panel de control comercial para monitorear el consumo de documentos de sus clientes.Sistema de alertas preventivas sobre el vencimiento físico de los certificados digitales de sus clientes.

Nivel 3: Clientes Finales (Empresas Emisoras):

Acceso Web: Configuración técnica inicial ante la DIAN (Carga del certificado digital .p12/.pfx, asociación de rangos de resoluciones de facturación y pegado del SoftwareID / Pin provistos por la DIAN). Consulta de documentos emitidos.

Acceso API: Credenciales exclusivas de integración para conectar sus ERPs o sistemas contables internos de manera directa.3. Tipos de Documentos Soportados ante la DIANEl backend del sistema procesará de manera nativa los siguientes esquemas XML según los anexos técnicos vigentes de la DIAN:

Grupo de Documento

Tipo de Documento XML

Código de Validación

Tipo de Envío

Facturación de Venta

Factura Electrónica de Venta, Nota Crédito, Nota Débito, Factura de Contingencia.CUFE (Código Único de Facturación Electrónica)Tiempo Real / Síncrono al negocioDocumentos EquivalentesTiquete POS Electrónico, Servicios Públicos, Extractos, Peajes, Boletas de Cine/Espectáculos, Notas de Ajuste DEE.CUDE (Código Único de Documento Equivalente)Tiempo Real / Síncrono al negocioSoportes de Costo/GastoDocumento Soporte en Adquisiciones a No Obligados, Nota de Ajuste de Documento Soporte.

CUDS (Código Único de Documento Soporte)Tiempo Real

Nómina Electrónica

Documento Soporte de Pago de Nómina Electrónica, Nota de Ajuste de Nómina.CUNE (Código Único de Nómina Electrónica)Lotes Consolidados (Primeros 10 días del mes)🩺 Submódulo Especial: Sector Salud (Resolución MinSalud)Para los clientes que operen en el sector médico (clínicas, laboratorios, médicos independientes), la API validará e inyectará obligatoriamente las extensiones XML de salud exigidas para el cruce automático con el RIPS:Mapeo de datos de la entidad responsable del pago (EPS/ARL) y modalidad de contratación.Datos de identificación del usuario/paciente.Nomenclatura oficial de servicios (Códigos CUPS).Desglose explícito de valores de Copagos o Cuotas Moderadoras restadas del total de la factura.4. Arquitectura de Infraestructura en Servidor LinuxPara garantizar la escalabilidad y alta disponibilidad sin recurrir a servicios de nube (AWS/Azure), el servidor Linux se estructurará de forma modular utilizando software de código abierto de alto rendimiento.





\[Cliente / ERP] ──(HTTPS JSON)──> \[ Nginx Reverse Proxy ] ──> \[ API Backend App ]

&#x20;                                                                    │

&#x20;                                                             (202 Accepted)

&#x20;                                                                    │

&#x20;                                                                    ▼

&#x20;                                                             \[ Redis Queue ]

&#x20;                                                                    │

&#x20;                                                                    ▼

&#x20;                                                           \[ Workers de Fondo ]

&#x20;                                                        (Abre .p12, Genera XML,

&#x20;                                                         Firma, Envía SOAP DIAN)

&#x20;                                                                    │

&#x20;                                                                    ▼

&#x20;                                                         \[ Webhook de Respuesta ]



A. Capa de Red y Proxy ReversoNginx: Actuará como la puerta de entrada única del servidor. Se encargará de la terminación SSL (gestión automática de certificados web con Let's Encrypt), balanceo de carga interno y mitigación de ataques básicos mediante políticas de Rate Limiting (por ejemplo, limitar a un máximo de 30 peticiones por segundo por API Key).

B. Capa de Aplicación y Cola Asíncrona (Resiliencia ante la DIAN)API Core: Desarrollado en un lenguaje de alta concurrencia (.NET Core) ejecutándose bajo un administrador de procesos (como PM2 o servicios de systemd nativos de Linux).Redis (Motor de Colas): Actúa como el amortiguador de tráfico. Cuando la API recibe una factura, valida la estructura básica del JSON, la guarda inmediatamente en la cola de Redis y responde al ERP del cliente un código HTTP 202 Accepted.Workers en Segundo Plano: Procesos dedicados que extraen las tareas de la cola de Redis de forma secuencial o paralela (según la capacidad de la CPU del servidor). Ellos asumen la carga pesada: generar el XML, firmarlo y conectarse por SOAP a la DIAN. Si la DIAN está lenta o caída, la cola retiene los documentos y aplica una política de reintentos automáticos con retroceso exponencial (exponential backoff), evitando que el sistema del cliente sufra caídas o bloqueos de pantalla.

C. Capa de Datos (Persistencia y Multi-Tenancy)SQL SERVER: Base de datos relacional robusta. El aislamiento de los datos se garantizará a nivel de software mediante un identificador estricto tenant\_id y client\_id en todas las tablas transaccionales. Se almacenarán aquí los logs de los estados de cada documento y las resoluciones numéricas.5. Bóveda de Seguridad Local y Firma DigitalAl almacenar certificados digitales de propiedad de terceros (.p12 o .pfx) en un servidor local, se implementa una política estricta de seguridad en el sistema de archivos de Linux:Aislamiento del Almacenamiento: Los archivos físicos de los certificados se guardarán en un directorio del sistema altamente restringido (ej. /var/secure/certificates/).Permisos de Linux: El directorio y los archivos tendrán permisos exclusivos chmod 700 y chmod 600. El propietario será únicamente el usuario del sistema operativo sin privilegios de root (ej. usuario dian\_app) que ejecuta los workers de la aplicación.Cifrado de Contraseñas: Las contraseñas para abrir los certificados jamás se guardarán en texto plano. Se almacenarán en la base de datos encriptadas con AES-256-CBC, utilizando una llave maestra de cifrado de 32 bytes almacenada únicamente como una variable de entorno protegida en el entorno global del servidor Linux.

Criptografía de Firma: Los workers utilizarán librerías nativas vinculadas a OpenSSL en Linux para procesar el certificado en memoria, extraer la llave privada de forma efímera, y realizar el firmado digital bajo el estándar XAdES-EPES directamente sobre los nodos del XML UBL 2.1 antes de enviarlo.6. Ciclo de Vida del Documento y Comunicación por WebhooksPara evitar conexiones HTTP colgadas debido a las latencias de respuesta de la DIAN, la comunicación con el Software Propio del cliente se realizará 100% mediante Webhooks asíncronos:1. ERP Cliente ───► Envía JSON con datos de la factura ───► API Portal

2\. ERP Cliente ◄─── Retorna HTTP 202 (ID de Transacción) ◄─── API Portal

&#x20;  \[El ERP del cliente libera la pantalla del usuario inmediatamente]

&#x20;  

3\. Worker Interno ──► Procesa Cola ──► Firma XML ──► Envía SOAP a la DIAN

4\. DIAN ──► Responde con ApplicationResponse (Aprobado/Rechazado) ──► Worker Interno

5\. Portal Web ──► Envía Notificación HTTP POST (Webhook) ──► ERP Cliente (Resultado Final)

Contrato del Webhook de Notificación (Ejemplo de payload enviado al cliente):JSON{

&#x20; "transaction\_id": "tx\_987654321\_abc",

&#x20; "document\_type": "01",

&#x20; "document\_number": "SETT1045",

&#x20; "status": "APPROVED",

&#x20; "dian\_code": "0",

&#x20; "dian\_description": "Documento validado por la DIAN exitosamente.",

&#x20; "cufe": "a9b8c7d6e5f4g3h2i1j0a9b8c7d6e5f4g3h2i1j0",

&#x20; "xml\_url": "https://tuportal.com/download/xml/factura\_1045.xml",

&#x20; "pdf\_url": "https://tuportal.com/download/pdf/factura\_1045.pdf"

}

7\. Plan de Habilitación Automatizado

El sistema incluirá en su módulo de Clientes Finales un script automatizado para la fase de Habilitación ante la DIAN.Al ingresar el SoftwareID y el Pin del cliente, la plataforma se encargará de estructurar el lote exacto de documentos de prueba requeridos por la DIAN (Facturas, Notas Crédito y Notas Débito de prueba), transmitirlos secuencialmente y verificar el cambio de estado en el portal de la DIAN hasta obtener el estado "Habilitado", permitiendo al cliente pasar a producción de forma autónoma.Este documento define la arquitectura base para dar inicio a la fase de diseño de software y base de datos en el servidor Linux.

