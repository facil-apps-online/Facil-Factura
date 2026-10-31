# Correo propio de la plataforma, plantillas editables y pendientes de certificados

Pendientes identificados el 2026-10-30 al poner en producción la venta de certificados por tenant (commit `80bf8b7`) y revisar cómo
salen hoy los correos. No se implementa en esta sesión: queda como tareas de desarrollo, en el orden sugerido.

## Contexto: cómo sale hoy el correo

`IEmailSender` (`SmtpEmailSender.cs`) resuelve el canal en orden: SMTP del cliente → SMTP del tenant → plataforma. La plataforma es
Core (`queue_platform_email` en Supabase), que encola en `client_email_queue` y lo envía por Brevo.

- **La plataforma no envía adjuntos.** Si el correo trae adjuntos y ni el cliente ni el tenant tienen SMTP, falla a propósito
  ("el envío por la plataforma todavía no soporta adjuntos"). Afecta:
  - el reenvío de documentos nativos (`InvoiceController.Resend`);
  - el `.p12` de los certificados (`CertificatePollingWorker`). Un tenant solo de certificados normalmente no tiene SMTP: el
    titular recibe **la contraseña sin el archivo** y no queda registro del fallo.
- **El reenvío no debe salir por Core.** Core nunca manejó adjuntos porque no había método nativo en uso. Aun así, `SmtpEmailSender`
  tiene un camino que encola el reenvío sin adjuntos en Core con la plantilla `invoice_resend`, que Core no tiene (su migración
  `20260907000001_add_platform_email_queue_and_templates.sql` solo siembra `password_reset` e `invitation`). Es código que sobra:
  se quita en E5 y el reenvío sale solo por los SMTP propios o por Brevo directo.
- **Contenido de los correos:**
  - armados en FacilFactura: documentos nativos y su reenvío, certificados, alertas de plan;
  - plantilla en Core, FacilFactura solo manda datos (`reset_link`, `user_name`, `logo_url`, `brand_name`): `password_reset` e
    `invitation`. Son HTML mínimo (logo centrado y tres párrafos).
- **`EmailWorker` es un esqueleto:** no envía nada, nadie encola en `fel:email:queue` y revisa la cola cada segundo.
- **Dataico tiene su propio canal** (API de Dataico) y queda fuera de todo esto.

## Decisiones tomadas

- Todos los correos de la plataforma salen de FacilFactura con el HTML armado aquí. Core deja de encolar y de tener plantillas
  para Facil Factura.
- Se respeta el orden de canales: SMTP del cliente → SMTP del tenant → Brevo de la plataforma. Los SMTP propios siguen siendo opción.
- El envío lo hace nuestro worker **al dispararse el evento**, no por un ciclo de tiempo.
- Las plantillas se guardan en la base de datos y se editan desde los portales.

---

## Tareas de correo

### E1. Bandeja de salida y envío por evento — prioridad alta

**Estado: implementado en local (migración `AddEmailOutbox`), sin commit ni despliegue.** Adjuntos y cuerpos sensibles se borran al enviarse; la contraseña de certificado va cifrada y retenida (`Held`) hasta que sale el correo del archivo.

- Tabla `EmailOutbox` con estos campos:
  - tipo de correo, tenant, cliente;
  - destinatario, asunto, HTML y adjuntos (en la tabla o en el almacenamiento de archivos según tamaño);
  - entidad origen (documento o solicitud de certificado);
  - estado (pendiente / enviando / enviado / fallido), intentos, próximo intento, último error;
  - canal usado y `messageId` del proveedor; fechas.
- **Encolado:** quien origina el correo inserta la fila en la misma transacción del cambio de negocio (patrón outbox). Al
  confirmar, publica el id en un canal de **Redis Pub/Sub**; Redis ya está conectado a las APIs y al worker.
- **`EmailWorker`:** se reescribe suscrito a ese canal y envía al instante.
- **Uso del tiempo, solo como red de seguridad:**
  - reintentos con espera creciente de los fallidos;
  - al arrancar, barrido de pendientes que quedaron (Redis caído, reinicio del worker).
- Idempotencia: un correo enviado no se reenvía por un reintento. Un reenvío intencional crea un correo nuevo (ver
  `2026-10-29-idempotencia-y-concurrencia-en-envios.md`).

### E2. Canal Brevo directo desde FacilFactura, con adjuntos — prioridad alta

**Estado: implementado en local, sin desplegar.** Falta pegar la API key en superadmin → Correo de la plataforma y probar con un envío real.

- **Llamada:** tercer canal de `IEmailSender`: `POST https://api.brevo.com/v3/smtp/email` con adjuntos en base64.
- **API key:** guardada **cifrada en la base de datos** con `ICryptoVault`, como las credenciales de Viafirma, y nunca en
  `appsettings.json` (repo público). Se pega desde superadmin:
  - tabla de configuración de la plataforma con la key cifrada;
  - pantalla de superadmin para guardarla, que solo muestra si está configurada (la API nunca la devuelve);
  - botón "Probar" que consulta la cuenta de Brevo.
- **Remitentes** (solo cuando sale por Brevo; con SMTP propio sale desde ese buzón):

  | Tipo | Nombre visible | Dirección | Responder a |
  |---|---|---|---|
  | Documentos nativos y reenvío | Nombre comercial del cliente | `facturacion@facil-factura.pro` | Correo de notificaciones del cliente |
  | Certificados | Nombre comercial del tenant | `certificados@facil-factura.pro` | Contacto del tenant |
  | Portal de clientes (invitación, contraseña, alertas de plan) | Nombre del tenant | `notificaciones@facil-factura.pro` | Contacto del tenant |
  | Plataforma (usuarios de tenant y superadmin) | Facil Factura | `no-responder@facil-factura.pro` | Soporte |

- **Verificar antes de empezar:**
  - dominio `facil-factura.pro` autenticado en Brevo (SPF/DKIM);
  - extensiones permitidas en adjuntos (`.zip`, `.pdf`, `.xml`; el `.p12` ya viaja comprimido);
  - límite de tamaño.
- **Opcional, después:** dominio propio por tenant verificado en Brevo.

### E3. Plantillas en base de datos y editores en las tres plataformas — prioridad media

- **Tipos:**
  - documento electrónico y reenvío;
  - certificado (archivo) y contraseña del certificado;
  - invitación, recuperación de contraseña;
  - alerta de plan.
- **Herencia:** plantilla de plataforma (superadmin) → personalización del tenant → personalización del cliente cuando aplique
  (documentos). Si un nivel no la tiene, se usa la del nivel superior.
- **Base común:** encabezado con logo y color del remitente, pie con sus datos.
- **Variables:** las que ya se mandan (`reset_link`, `user_name`, `logo_url`, `brand_name`) más las de cada tipo (número de
  documento, adquirente, titular, vencimiento...).
- **Editores:** en superadmin (plantillas de plataforma), en el portal de tenant (las suyas y las de sus clientes) y en el portal
  de clientes (las de sus documentos). Cada editor con vista previa y "enviarme una prueba".
- **Referencias:** Core ya tiene un modelo parecido (`email_templates` con `is_customizable` / `is_disableable` y acciones
  `*_platform_email_template`). Los editores de plantillas PDF del tenant (`TemplateEditor`) sirven de base para la interfaz.
- **Restricción DIAN:** el asunto de los documentos tiene el formato del anexo técnico y no debe ser editable.

### E4. Sacar invitaciones y contraseñas de Core — prioridad media (después de E1-E3)

- **Plantillas:** sembrar `password_reset` e `invitation` en nuestra base con el contenido actual de Core, adaptado a la base común.
- **`PasswordResetService`:** encola en la bandeja propia en vez de `QueuePlatformEmailAsync`. Con usuarios de un cliente prueba
  sus SMTP; con tenant, superadmin y developers va directo a Brevo.
- Retirar `QueuePlatformEmailAsync` y el uso de `client_email_queue` para Facil Factura.

### E5. Reenvío de documentos nativos — prioridad alta (junto con E1-E2)

**Estado: implementado en local, sin desplegar.** Pendiente: confirmar el asunto DIAN y el contenido del ZIP del envío inicial.

- Quitar de `SmtpEmailSender` el camino a Core (`QueuePlatformEmailAsync` con `invoice_resend`). El reenvío sale por SMTP del
  cliente → SMTP del tenant → Brevo directo, nunca por Core.
- El reenvío responde "En cola" y el documento muestra **Enviado** o **Falló (motivo)** con fecha y canal.
- Confirmar el asunto en formato DIAN y el ZIP con AttachedDocument y PDF en el envío inicial y en el reenvío.

### E6. Correo de certificados — prioridad alta (junto con E1-E2)

**Estado: implementado en local, sin desplegar.**

- La contraseña solo se encola cuando el correo con el `.p12` quedó **enviado**.
- El resultado queda como evento de la solicitud y se muestra en Solicitudes ("no se pudo enviar al titular; descárgalo aquí").
- Respeta el interruptor del tenant "Enviar el certificado al titular por correo" (`Tenant.SendCertificateToHolderByEmail`).

---

## Pendientes de certificados

### C1. Mensaje real del error de sincronización de perfiles — listo en local, sin desplegar

`SuperadminCertificatesController.SyncProfiles` devuelve el motivo real: 400 si falta configuración del ambiente, 500 con la causa
si es otro error, incluido el mensaje de SQL Server. Falta commit y despliegue de `fel-api-superadmin`.

### C2. Credenciales de producción de Viafirma — operación

"Probar Producción" responde *"El proveedor de certificados no tiene configurado el nombre de un secreto"*: no hay consumer key y
secret de producción guardados. Pegarlos en Proveedor de Certificados → Credenciales Producción y sincronizar los perfiles de
producción. Sin eso no se pueden crear precios.

### C3. Largo del campo `Key` de `CertificateProfiles` — prioridad media

`Key` es `{proveedor}:{ambiente}:{código}` con máximo 100 caracteres, mientras `ExternalCode` admite 500. En Producción el prefijo
es 3 caracteres más largo que en Sandbox; un código largo de Viafirma desbordaría al sincronizar. Ampliar a 600 o derivar `Key`
con un hash del código.

### C4. Tenants que ya facturan con emisión nativa — comunicación

Desde `80bf8b7` sus solicitudes de producción exigen precio vigente y saldo. Configurar precios y abonos antes de que soliciten, o
marcarlos "Exento de cobro".

### C5. Menores

- Exponer `Content-Disposition` en CORS para que la descarga del `.p12` tome el nombre del servidor. Hoy usa un nombre armado en
  el navegador.
- Correos propios de FacilFactura para el KYC de Viafirma (especificación §12.1), en vez de depender del correo de Viafirma.

### C6. Fases siguientes de la especificación

- Renovación automática (fase 4): programación, certificado futuro, rollover y alertas de vencimiento.
- Pago por pasarela (por ejemplo Wompi) sobre el mismo modelo de saldo.
