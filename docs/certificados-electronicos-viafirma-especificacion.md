# Especificación del ciclo de vida de certificados electrónicos

## 1. Propósito y alcance

Diseñar e implementar la adquisición, emisión, instalación, renovación, reemplazo, revocación y facturación de certificados electrónicos para los clientes emisores de cualquier tenant mediante Viafirma RA Colombia y su API de perfiles PKCS#10.

El sistema debe permitir que un tenant solicite certificados desde el portal, que el estado se sincronice automáticamente, que el cliente complete KYC o aporte documentos cuando sea necesario, que el P7B se convierta en un P12 utilizable por la firma DIAN y que una renovación se prepare antes del vencimiento sin interrumpir la emisión.

El precio, la ventana de renovación, el momento de causar el cargo, los perfiles y los ambientes deben ser configurables desde administración. No deben existir códigos de perfiles, URLs, precios, días de anticipación, estados de negocio ni mensajes críticos hardcodeados.

## 2. Fuentes analizadas

- `Backlog/Uso del API para perfiles PKCS10 v1.8.pdf`, versión 1.8 del 18/09/2026.
- `Backlog/RA Colombia - PKCS10 API.postman_collection v1.8.json`.
- `src/Fel.Core/Entities/Certificate.cs`.
- `src/Fel.Api.Tenant/Controllers/TenantCertificatesController.cs`.
- `src/Fel.Api.Client/Controllers/CertificateController.cs`.
- `src/Fel.Infrastructure/Security/CertificateStorageService.cs` y `CryptoVault.cs`.
- `src/Fel.Worker/Worker.cs` y los workers periódicos existentes.
- `src/Fel.Infrastructure/Services/BillingMetricsService.cs` y `MonthlyBillingCutService.cs`.
- `apps/tenant-web/src/pages/Certificates.tsx`, `ClientEdit.tsx` y `Dashboard.tsx`.

## 3. Hallazgos actuales

### 3.1 Se puede reutilizar

- Existe `Certificate` relacionado con `Client`.
- El archivo se guarda fuera de la base y la contraseña se cifra.
- El worker busca el certificado activo para firmar documentos.
- Ya existen listado tenant, listado Superadmin, workers y corte mensual.
- El sistema ya tiene modelos de tarifas y consumos por tenant, cliente e integrador.

### 3.2 Deuda técnica actual

1. La fecha de vencimiento se fija actualmente con `DateTime.UtcNow.AddYears(1)` en vez de leer el certificado real.
2. `Certificate` no conoce proveedor, ambiente, solicitud externa, perfil, versión, renovación ni certificado anterior.
3. La carga manual no valida completamente clave privada, correspondencia con cliente, NIT, fechas, EKU ni capacidad real de firma.
4. Hay lógica duplicada en los controladores de carga tenant y cliente.
5. No existe cliente abstracto del proveedor, máquina de estados, idempotencia ni jobs específicos.
6. No existe facturación de certificados ni proyección para el dashboard tenant.
7. Hay fallbacks literales de tarifas en billing, como `70m`; el módulo nuevo no debe repetir ese patrón.
8. Debe reforzarse la autorización por tenant; un `clientId` recibido por header o ruta nunca debe ser suficiente sin validar pertenencia y permisos.

## 4. Restricción esencial de Viafirma

El manual no describe una entrega directa de P12. Describe este proceso:

1. El integrador genera un CSR y conserva la clave privada.
2. Envía el CSR a `POST /request/fromCSR`.
3. Viafirma ejecuta validación de identidad, KYC, documentación y aprobación de operadores.
4. El integrador descarga el P7B firmado cuando el estado lo permite.
5. El integrador finaliza localmente el certificado con la clave privada original.

Viafirma confirmó que FacilFactura puede generar y custodiar la clave privada en una bóveda segura. La implementación debe mantener esa custodia únicamente en backend, con cifrado, control de acceso y sin exponerla al portal ni a los correos.

### Alternativas

| Alternativa | Descripción | Evaluación |
|---|---|---|
| A. Backend seguro | FacilFactura genera la clave y CSR, guarda la clave en vault y ensambla el P12 al descargar el P7B. | Recomendada para renovación automática, sujeta a aprobación de Viafirma. |
| B. Equipo del solicitante | Navegador o aplicación local genera la clave y CSR; el usuario participa en cada instalación. | Más cercana a custodia local, pero dificulta renovación automática. |

El código debe aislar esta decisión detrás de `ICertificateKeyMaterialService`, para poder cambiar la estrategia sin reescribir el ciclo de vida.

## 5. Recursos de Viafirma

| Operación | Método y ruta |
|---|---|
| Perfiles disponibles | `GET /ra/available-profiles?codRa={ra}` |
| Campos de perfil | `GET /ra/profile/{codProfile}/form?required=true` |
| Crear solicitud | `POST /request/fromCSR` |
| Estado básico | `GET /request/{codRequest}/status` |
| Estado avanzado | `GET /request/{codRequest}/advancedStatus` |
| Descargar P7B | `GET /downloadCertificateServlet?req={publicId}` |
| Código de revocación | `GET /request/{codRequest}/revocationCode` |
| Revocar | `POST /request/revoke/code/{revokingCode}` |
| Link KYC | `GET /services/accreditation/{codRequest}` |
| Subir anexos | `POST /files/upload/` |
| Listar anexos | `GET /files/list/{codRequest}` |
| Rechazar | `PUT /request/reject/{codRequest}` |

Los códigos de perfil cambian entre Sandbox y Producción. Deben sincronizarse y almacenarse por ambiente, nunca copiarse a constantes.

## 6. Actores y responsabilidades

| Actor | Responsabilidad |
|---|---|
| Superadmin | Proveedor, perfiles, precios, ventanas, políticas, reintentos y visión global. |
| Tenant | Solicitudes para sus clientes, configuración de renovación y consulta de cargos. |
| Cliente emisor | Datos, aceptación de términos, KYC y documentos solicitados. |
| Worker | Sincronización, descarga, ensamblaje, activación, renovación y notificaciones. |
| Viafirma RA | Validación, revisión y firma del certificado. |
| DIAN | Uso del certificado para firmar y emitir. |

## 7. Casos de uso

### CU-01. Configurar el proveedor

1. Superadmin selecciona Sandbox o Producción.
2. Define URLs, código de RA y referencias a secretos.
3. Pulsa `Probar conexión`.
4. El sistema consulta perfiles autorizados.
5. Guarda códigos, títulos, vigencia informativa, tipo, token, URL de términos y fecha de sincronización.

Reglas: consumer secret solo en secret store; no mostrarlo en frontend; perfiles separados por ambiente; guardar hash de la respuesta para detectar cambios.

### CU-02. Administrar precios

1. Superadmin selecciona proveedor, perfil, ambiente y moneda.
2. Define precio de compra, precio al tenant, impuestos y fecha de vigencia.
3. El sistema impide períodos superpuestos.
4. Cada cambio crea una nueva versión, sin modificar historial.

El precio vigente se copia a la orden y al cargo al crear la solicitud o en el evento definido por `ChargeTrigger`.

### CU-03. Solicitar certificado inicial

1. Tenant selecciona cliente y perfil natural o jurídico.
2. El sistema carga los campos dinámicos del proveedor y aplica validaciones locales.
3. Genera material criptográfico y CSR con la estrategia aprobada.
4. El usuario acepta los términos recibidos en el perfil.
5. Guarda evidencia de aceptación: URL, hash, usuario, fecha UTC, IP y user agent.
6. Envía `POST /request/fromCSR`.
7. Guarda `codRequest`, `publicId`, perfil, ambiente y estado.
8. El worker continúa el proceso.

### CU-04. Completar KYC

1. El sistema detecta `accreditation` o `accreditation_rejected`.
2. Solicita el link KYC.
3. Lo envía al suscriptor y lo muestra con fecha de obtención.
4. El usuario completa la verificación.
5. El worker consulta estado avanzado.

No se debe prometer aprobación automática: la revisión de operadores puede ser manual.

### CU-05. Aportar documentos

1. Al detectar `docRequired`, se muestran las notas públicas.
2. El usuario adjunta el documento requerido.
3. Se validan MIME, extensión, tamaño y antivirus.
4. Se envía base64 a `/files/upload/`.
5. Se guarda hash, tamaño, actor, identificador remoto y fecha.
6. Se vuelve a sincronizar la solicitud.

### CU-06. Descargar e instalar

1. Detectar `Generated_Not_Downloaded`, `Generated_And_Downloaded` o `signedContract`.
2. Descargar P7B idempotentemente.
3. Validar que la clave pública coincida con el CSR.
4. Ensamblar P12 con la clave privada y la cadena.
5. Leer el certificado real y validar fechas, NIT, sujeto, EKU y clave privada.
6. Guardar el archivo en almacenamiento privado.
7. En emisión inicial, activarlo; en renovación, dejarlo como futuro.
8. Crear cargo según la política.

### CU-07. Renovar automáticamente

1. Consultar diariamente certificados activos con renovación habilitada.
2. Calcular `renewalStartAt = notAfter - renewalLeadTimeDays`.
3. Crear solicitud solo si no existe una solicitud activa para el mismo cliente y ambiente.
4. Generar nueva clave y CSR, salvo política aprobada por Viafirma.
5. Repetir KYC, términos y documentos cuando el proveedor lo exija.
6. Descargar, ensamblar y validar el nuevo P12.
7. Prepararlo como certificado futuro.
8. Activarlo en la fecha configurada, normalmente antes del vencimiento actual.
9. Retirar el anterior solo después de validar el nuevo.

Si la renovación queda bloqueada por KYC, documentos o revisión manual, se deben enviar alertas escalonadas. No es técnicamente posible garantizar continuidad si la aprobación externa no ocurre a tiempo.

### CU-08. Revocar

1. Obtener `revocationCode`.
2. Confirmar motivo y acción irreversible.
3. Ejecutar revocación.
4. Marcar `RevocationRequested`.
5. Sincronizar y marcar `Revoked` cuando corresponda.

### CU-09. Rechazar solicitud

El rechazo es definitivo según el manual. Antes de llamar al proveedor se debe comprobar que el estado sea compatible, advertir que no hay reversión y registrar la razón local.

### CU-10. Consultar consumo

El tenant debe ver certificados adquiridos, renovaciones, cargos causados del mes, documentos, total causado, proyección siguiente y vencimientos próximos. La proyección debe estar marcada como estimada.

## 8. Máquina de estados

### 8.1 Estado local de la solicitud

| Estado local | Estados de Viafirma | Significado |
|---|---|---|
| `Draft` | Ninguno | Datos incompletos. |
| `Submitted` | Solicitud creada | Enviada al proveedor. |
| `WaitingForIdentity` | `accreditation` | Falta KYC. |
| `IdentityReview` | `accreditation_check`, `checking`, `collate_data` | Validación o revisión. |
| `DocumentsRequired` | `docRequired` | Falta documentación. |
| `DocumentsSubmitted` | `docUploaded` | Documentación enviada. |
| `ProviderReview` | `proposeFor`, `proposedToAcceptance`, `All_Ok` | Revisión de la RA. |
| `CertificateProcessing` | `inProcess`, `Cite_To_Finish`, `processingContract` | Firma o generación. |
| `ReadyToDownload` | `Generated_Not_Downloaded`, `signedContract` | P7B disponible. |
| `Downloaded` | `Generated_And_Downloaded` | P7B descargado. |
| `Installing` | Interno | Ensamblando y validando P12. |
| `InstalledPendingActivation` | Interno | P12 válido, reservado para renovación. |
| `Active` | Interno | Certificado vigente y utilizable. |
| `Rejected` | `rejected`, rechazo definitivo | No aprobado o cancelado. |
| `Failed` | `fail` o error local | Reintento o soporte. |
| `RevocationRequested` | Interno | Revocación solicitada. |
| `Revoked` | Confirmación | Revocado. |
| `Expired` | Interno | Vencido sin sustituto activo. |

La interfaz debe mostrar traducciones de negocio, no los códigos crudos del proveedor.

### 8.2 Estado del certificado

- `Current`: certificado que firma actualmente.
- `Future`: instalado y listo para próximo rollover.
- `Retired`: histórico, ya no se usa.
- `Revoked`: revocado.
- `Invalid`: no pasó validación.

Debe existir como máximo un `Current` por cliente y ambiente. La activación debe usar lock por `ClientId` y ambiente.

## 9. Modelo de datos

### 9.1 `Certificate`

Agregar a la entidad actual:

| Campo | Uso |
|---|---|
| `Environment` | `Sandbox` o `Production`. |
| `ProviderKey` | Proveedor configurado, por ejemplo `viafirma`. |
| `ProfileId` | Perfil local utilizado. |
| `CertificateRequestId` | Solicitud que lo originó. |
| `Thumbprint`, `SerialNumber`, `Subject`, `Issuer` | Metadatos reales del certificado. |
| `NotBefore`, `NotAfter` | Fechas leídas del certificado, no calculadas. |
| `ActivationAt` | Momento planificado para rollover. |
| `Status` | Estado de instalación. |
| `ActivatedAt`, `RetiredAt`, `RevokedAt` | Auditoría. |

Índices: `ClientId + Environment + Status`, `Thumbprint` único por ambiente y `NotAfter` para jobs. `IsActive` debe mantenerse temporalmente por compatibilidad, pero la fuente de verdad debe ser `Status`.

### 9.2 `CertificateProvider`

Catálogo para desacoplar el dominio de Viafirma:

- `Key`, `Name`, `IsActive`.
- URL Sandbox, URL Producción y URL de descarga.
- Código de RA.
- Referencias a secretos, nunca secretos planos.
- Esquema de autenticación, timeouts, retries y circuit breaker.

### 9.3 `CertificateProfile`

Persistir por proveedor y ambiente: `ExternalCode`, `Key`, `Title`, `Description`, `PersonType`, `ValidityDays`, `TermsUrl`, `TermsHash`, `ExternalType`, `TokenType`, `IsActive` y `LastSyncedAt`.

### 9.4 `CertificateProfileField`

Persistir la definición de `/form?required=true`: `ExternalName`, `Label`, `Type`, `Regex`, `IsRequired`, `IsEditable`, `IsUsed`, `DefaultValue`, `DisplayOrder`, `DefinitionHash` y `FetchedAt`.

La validación dinámica no reemplaza las validaciones de seguridad. Las regex del proveedor deben compilarse con protección contra expresiones costosas.

### 9.5 `CertificateRequest`

Campos mínimos:

- `ClientId`, `TenantId`, `Environment`, `ProfileId`.
- `RequestType`: `Initial`, `Renewal`, `Replacement`.
- `PreviousCertificateId`.
- `CodRequest`, `PublicId` protegido según sensibilidad.
- `ExternalStatus`, `NormalizedStatus`, `AdvancedStatus`.
- `AccreditedStatus`, `PaymentStatus`, `KycUrl`.
- `CsrReference`, `CsrHash`, `PublicKeyHash`.
- `TermsUrl`, `TermsHash`, `TermsAcceptedAt`, `TermsAcceptedBy`.
- Fechas de envío, aprobación, descarga, instalación y finalización.
- `LastProviderSyncAt`, `NextProviderSyncAt`.
- `RetryCount`, `NextRetryAt`, `LastErrorCode`, `LastErrorMessage`.
- `IdempotencyKey`, usuario creador y timestamps.

### 9.6 `CertificatePrice`

Campos: `ProviderId`, `ProfileId`, `Environment`, `TenantId` opcional, `PriceType`, `Currency`, `NetAmount`, `TaxRate`, `EffectiveFrom`, `EffectiveTo`, `IsActive`, usuario creador y timestamps.

No permitir vigencias superpuestas para la misma combinación comercial.

### 9.7 `CertificateCharge`

Campos: `TenantId`, `ClientId`, `CertificateRequestId`, `ChargeType`, `PriceListId`, `UnitPrice`, `TaxAmount`, `TotalAmount`, `Currency`, período, `Status`, `OccurredAt` y descripción histórica.

Crear un índice lógico único por solicitud y tipo de cargo. Los reintentos técnicos no deben duplicar importes.

### 9.8 `CertificateEvent`

Bitácora append-only con solicitud, certificado, evento, estado anterior y nuevo, estado externo, HTTP status, correlation id, idempotency key, actor, timestamp y metadata JSON sanitizada.

## 10. CSR y material criptográfico

### FE-PJ

El manual requiere: `C` país ISO 3166, `ST` departamento, `L` ciudad, `STREET` dirección, `O` organización, `OU` unidad organizacional, `SERIALNUMBER` NIT, `E` email, `GN` nombre del representante y `SN` apellidos.

### FE-PN

Requiere: `C`, `ST`, `L`, `STREET`, `SERIALNUMBER` de identidad, `E`, `GN` y `SN`.

### Reglas

- Una clave nueva por solicitud y renovación.
- Algoritmo y tamaño aprobados por Viafirma.
- No registrar clave privada, CSR completo, P12, password ni base64.
- Guardar hashes para correlación.
- Validar coincidencia de clave pública entre CSR y P7B.
- Generar una password fuerte de 16 caracteres alfanuméricos y símbolos, con longitud, conjunto de caracteres y política configurables mediante configuración segura.
- Guardar la password cifrada o como referencia a un secreto en vault. Para exportar el P12, descifrarla únicamente en memoria y nunca persistirla en texto plano.
- Almacenar archivo con nombre aleatorio y permisos privados.
- Validar MIME, extensión, tamaño y antivirus de anexos.

## 11. Abstracción del proveedor

Crear en Core una interfaz equivalente a:

```csharp
public interface ICertificateProvider
{
    Task<IReadOnlyList<ProviderCertificateProfile>> GetProfilesAsync(CertificateProviderContext context, CancellationToken ct);
    Task<IReadOnlyList<ProviderProfileField>> GetProfileFieldsAsync(string profileCode, CertificateProviderContext context, CancellationToken ct);
    Task<ProviderRequestCreated> CreateRequestFromCsrAsync(CreateProviderRequest command, CancellationToken ct);
    Task<ProviderRequestStatus> GetStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken ct);
    Task<ProviderAdvancedStatus> GetAdvancedStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken ct);
    Task<string> GetKycLinkAsync(string requestCode, CertificateProviderContext context, CancellationToken ct);
    Task<ProviderFile[]> UploadFilesAsync(string requestCode, IReadOnlyList<ProviderFileUpload> files, CancellationToken ct);
    Task<byte[]> DownloadP7bAsync(string publicId, CertificateProviderContext context, CancellationToken ct);
    Task<string> GetRevocationCodeAsync(string requestCode, CertificateProviderContext context, CancellationToken ct);
    Task RevokeAsync(string revocationCode, CertificateProviderContext context, CancellationToken ct);
    Task RejectAsync(string requestCode, CertificateProviderContext context, CancellationToken ct);
}
```

Implementar `ViafirmaPkcs10Provider` en Infrastructure. Ningún controlador debe construir URLs, conocer códigos de perfil o firmar OAuth directamente.

### HTTP y reintentos

- OAuth 1.0 en un `DelegatingHandler`, según el manual.
- Secretos resueltos por secret store.
- Reintentar timeout, 408, 429 y 5xx con backoff y jitter.
- No reintentar automáticamente 400, 401, 403, 404 ni errores de negocio.
- Circuit breaker por proveedor y ambiente.
- Registrar requests y responses sanitizados.
- Usar idempotency key local para toda operación con efecto.

## 12. Jobs y orquestación

Crear un `CertificateLifecycleWorker` con tareas reanudables y pequeñas:

1. `SyncProviderProfilesJob`.
2. `SyncPendingCertificateRequestsJob`.
3. `FetchKycLinksJob`.
4. `DownloadReadyP7bJob`.
5. `AssembleAndValidateCertificateJob`.
6. `ScheduleRenewalsJob`.
7. `ActivateDueCertificatesJob`.
8. `ExpireCertificatesJob`.
9. `NotifyCertificateEventsJob`.
10. `ReconcileCertificateChargesJob`.

Cada job debe trabajar por lotes, usar lease temporal, liberar el lease ante error y ser seguro si se ejecuta dos veces.

### Frecuencias iniciales configurables

| Job | Frecuencia |
|---|---|
| Solicitudes pendientes | Cada 5 minutos |
| P7B listo | Cada 2 minutos |
| Renovaciones | Cada hora |
| Activación | Cada minuto |
| Alertas de vencimiento | Diaria |
| Perfiles | Diaria y bajo demanda |
| Conciliación de cargos | Diaria |

### Política de renovación

Configuración por política, no constantes:

- `RenewalLeadTimeDays`.
- `ActivationSafetyMarginMinutes`.
- `MinimumRemainingDaysForAutoRenewal`.
- `MaxRenewalAttempts`.
- `RenewalRetrySchedule`.
- `RequireKycForRenewal`.
- `AutoRenewalEnabledByDefault`.

Calcular con la fecha real `NotAfter`, normalizar UTC y mostrar en la zona del tenant.

### Activación segura

1. Crear solicitud.
2. Aprobar y descargar P7B.
3. Ensamblar P12.
4. Validar P12, CSR, NIT y firma local.
5. Guardar nuevo material.
6. Tomar lock de cliente y ambiente.
7. Activar nuevo certificado.
8. Retirar el anterior.
9. Registrar evento y notificar.

Si falla antes del paso 7, el certificado actual permanece activo.

## 12.1 Orquestación de correos por FacilFactura

Viafirma puede ejecutar el proceso del proveedor, pero la comunicación con el tenant y el cliente debe salir de FacilFactura. El sistema no debe depender del correo automático de Viafirma como canal principal de experiencia, porque necesitamos controlar idioma, marca, trazabilidad, reintentos y acciones disponibles.

Se debe reutilizar el servicio de correo existente (`IEmailSender` y `SmtpEmailSender`) detrás de un servicio de dominio específico:

```csharp
public interface ICertificateNotificationService
{
    Task NotifyRequestCreatedAsync(Guid requestId, CancellationToken ct);
    Task NotifyKycRequiredAsync(Guid requestId, Uri kycUrl, CancellationToken ct);
    Task NotifyDocumentsRequiredAsync(Guid requestId, CancellationToken ct);
    Task NotifyProviderReviewAsync(Guid requestId, CancellationToken ct);
    Task NotifyCertificateReadyAsync(Guid requestId, CancellationToken ct);
    Task NotifyCertificateActivatedAsync(Guid certificateId, CancellationToken ct);
    Task NotifyRenewalScheduledAsync(Guid certificateId, CancellationToken ct);
    Task NotifyRenewalAtRiskAsync(Guid certificateId, CancellationToken ct);
    Task NotifyCertificateExpiredAsync(Guid certificateId, CancellationToken ct);
}
```

### 12.1.1 Eventos que generan correo

| Evento | Destinatario | Acción principal |
|---|---|---|
| Solicitud creada | Tenant y representante | Consultar estado. |
| KYC requerido | Representante del cliente y tenant | `Continuar verificación`. |
| KYC no coincide | Representante y tenant | Revisar datos o contactar soporte. |
| Documentos requeridos | Representante y tenant | `Cargar documentos`. |
| Solicitud en revisión | Tenant | Consultar avance. |
| Certificado emitido | Tenant | Consultar instalación. |
| Certificado activado | Tenant y cliente | Confirmar que puede emitir. |
| Renovación programada | Tenant | Consultar fecha y configuración. |
| Renovación en riesgo | Tenant y soporte | Resolver la acción pendiente. |
| Renovación completada | Tenant y cliente | Confirmar continuidad. |
| Vencimiento próximo | Tenant y cliente | Revisar renovación. |
| Certificado expirado | Tenant y Superadmin | Atención prioritaria. |
| Revocación confirmada | Tenant y cliente | Informar que ya no puede firmar. |

### 12.1.2 Diseño técnico del envío

- Crear una tabla `NotificationOutbox` o reutilizar una outbox existente.
- La transición de estado y la creación del mensaje se guardan en la misma transacción local.
- Un `NotificationWorker` toma mensajes pendientes y llama a `IEmailSender`.
- Usar `NotificationType`, `EntityId`, `Recipient`, `TemplateKey`, `TemplateVersion`, `PayloadJson`, `Status`, `Attempts`, `NextAttemptAt`, `LastError` y timestamps.
- Usar clave única por `EntityId + NotificationType + Milestone`, salvo notificaciones repetibles como recordatorios.
- Reintentar errores transitorios con backoff; no duplicar correos por reinicio del worker.
- Registrar `MessageId`, `CorrelationId`, destinatario enmascarado, plantilla y resultado.
- No incluir passwords, claves privadas, P12, CSR completo ni tokens persistentes como adjuntos de correo.
- Los enlaces deben ser URLs de FacilFactura con token corto, expiración y un solo uso cuando representen una acción sensible.
- No enviar directamente una URL de Viafirma si se puede intermediar con una ruta propia que registre la apertura y redirija de forma segura.

### 12.1.4 Entrega del P12 al cliente

El cliente debe poder descargar el P12 completo, incluyendo la clave privada, para utilizarlo fuera de FacilFactura. Esta exportación no debe confundirse con la custodia operativa interna.

1. Crear una exportación bajo demanda desde el portal autorizado del tenant o cliente.
2. Validar permisos, identidad, cliente, ambiente y certificado activo.
3. Generar un paquete P12 en memoria usando el certificado, la clave privada y la password fuerte de 16 caracteres.
4. Entregarlo mediante descarga autenticada de un solo uso, con expiración configurable, auditoría y límites de descarga.
5. Mostrar o entregar la password por un canal controlado separado del archivo. Nunca incluirla en logs, URL, nombre del archivo ni en el mismo correo que contiene el enlace de descarga.
6. Registrar actor, fecha, certificado, motivo, IP, resultado y huella del archivo, sin registrar el contenido del P12 ni la password.
7. Permitir revocar el enlace antes de su uso y generar una nueva exportación si el cliente pierde la password.

### 12.1.3 Acción KYC desde nuestro servicio

El correo debe llevar a una ruta propia, por ejemplo `/certificate-requests/{id}/kyc`, que:

1. Valide el token y su expiración.
2. Compruebe que la solicitud todavía está en estado compatible.
3. Registre apertura y actor.
4. Obtenga o renueve el enlace KYC con Viafirma si es necesario.
5. Redirija al proveedor sin exponer credenciales internas.
6. Muestre una pantalla de retorno para continuar consultando el estado.

La URL KYC no debe almacenarse ni reutilizarse indefinidamente. Debe renovarse desde backend cuando haya expirado.

### 12.1.4 Plantillas mínimas

Las plantillas deben estar versionadas y administrables, con asunto, texto plano, HTML, idioma y marca del tenant:

- `certificate-request-created`.
- `certificate-kyc-required`.
- `certificate-documents-required`.
- `certificate-provider-review`.
- `certificate-ready`.
- `certificate-activated`.
- `certificate-renewal-scheduled`.
- `certificate-renewal-at-risk`.
- `certificate-expired`.
- `certificate-revoked`.

Variables permitidas: nombre del tenant, nombre del cliente, estado visible, fecha de vencimiento, fecha de renovación, enlace de acción, soporte y número de solicitud interno. Las plantillas no deben permitir interpolar valores arbitrarios del proveedor sin sanitización.

### 12.1.5 Recordatorios

Los recordatorios deben ser configurables y no repetirse indefinidamente. Política inicial propuesta:

- Primer aviso al entrar en ventana de renovación.
- Recordatorio cuando falten 14 días.
- Recordatorio cuando falten 7 días.
- Alerta diaria desde el umbral de riesgo configurado.
- Aviso inmediato si la renovación queda bloqueada.
- Confirmación al activar el nuevo certificado.

Todos los umbrales deben estar en configuración, no en código.

## 13. API interna propuesta

### Tenant

| Método | Ruta | Propósito |
|---|---|---|
| `GET` | `/api/tenant/certificates` | Lista de certificados de sus clientes. |
| `GET` | `/api/tenant/clients/{clientId}/certificate-lifecycle` | Estado detallado. |
| `POST` | `/api/tenant/clients/{clientId}/certificate-requests` | Solicitud inicial o reemplazo. |
| `POST` | `/api/tenant/certificate-requests/{id}/renew` | Renovación manual. |
| `GET` | `/api/tenant/certificate-requests/{id}` | Estado, acciones y notas. |
| `POST` | `/api/tenant/certificate-requests/{id}/kyc-link` | Obtener link KYC. |
| `POST` | `/api/tenant/certificate-requests/{id}/files` | Subir anexo. |
| `POST` | `/api/tenant/clients/{clientId}/certificate/auto-renewal` | Activar renovación. |
| `GET` | `/api/tenant/dashboard/certificate-billing` | Consumo y proyección. |

### Cliente

| Método | Ruta | Propósito |
|---|---|---|
| `GET` | `/api/v1/certificates/me` | Estado propio. |
| `POST` | `/api/v1/certificate-requests/{id}/terms` | Aceptar términos. |
| `GET` | `/api/v1/certificate-requests/{id}/kyc` | Acción KYC. |
| `POST` | `/api/v1/certificate-requests/{id}/files` | Subir documento. |
| `GET` | `/api/v1/dashboard/summary` | Resumen de actividad y cobros. |

### Superadmin

| Método | Ruta | Propósito |
|---|---|---|
| `GET` | `/api/superadmin/certificate-providers` | Proveedores. |
| `POST` | `/api/superadmin/certificate-providers/{id}/test` | Probar conexión. |
| `POST` | `/api/superadmin/certificate-providers/{id}/sync-profiles` | Sincronizar perfiles. |
| `GET` | `/api/superadmin/certificate-prices` | Precios versionados. |
| `POST` | `/api/superadmin/certificate-prices` | Crear precio. |
| `PUT` | `/api/superadmin/certificate-prices/{id}` | Cerrar vigencia. |
| `GET` | `/api/superadmin/certificate-requests` | Solicitudes globales. |
| `GET` | `/api/superadmin/certificate-billing` | Cargos y conciliación. |
| `POST` | `/api/superadmin/certificate-requests/{id}/retry` | Reintento recuperable. |
| `POST` | `/api/superadmin/certificates/{id}/revoke` | Revocación administrativa. |

## 14. Facturación y dashboard tenant

### 14.1 Momento de causar el cargo

Recomendación: causar el cargo cuando Viafirma haya aprobado la solicitud y el P7B se haya descargado correctamente, antes de activar el P12. Así no se cobra una solicitud rechazada y se reconoce el costo cuando el certificado ya fue emitido.

Debe existir una configuración `ChargeTrigger` con valores:

- `RequestCreated`.
- `ProviderApproved`.
- `P7bDownloaded`.
- `CertificateActivated`.

La política no cambia cargos históricos.

### 14.2 Precio histórico

El cargo copia `PriceListId`, precio unitario, impuestos, moneda, descripción, cliente, tenant, perfil y ambiente. Un cambio de precio solo aplica a solicitudes posteriores a su vigencia.

Crear una restricción lógica única por `CertificateRequestId + ChargeType`, ignorando cargos anulados. Usar transacción e idempotencia para que dos workers no dupliquen cargos.

### 14.3 Dashboard tenant

Agregar al dashboard existente un bloque con:

| Indicador | Significado |
|---|---|
| `Cargos de certificados este mes` | Cargos causados en el período. |
| `Certificados activos` | Clientes con certificado vigente. |
| `Renovaciones próximas` | Renovaciones dentro de la ventana. |
| `Requieren atención` | KYC, documentos, errores o vencimientos en riesgo. |
| `Proyección próximo mes` | Documentos esperados más renovaciones conocidas. |

El detalle debe agrupar por cliente, perfil, cargo, estado y fecha. El frontend debe consumir un endpoint agregado y no calcular importes recorriendo certificados.

Respuesta sugerida:

```json
{
  "period": { "year": 2026, "month": 9 },
  "currency": "COP",
  "currentMonth": {
    "documentCharges": 120000,
    "certificateCharges": 350000,
    "otherCharges": 0,
    "totalAccrued": 470000
  },
  "nextMonthProjection": {
    "documentCharges": 90000,
    "certificateCharges": 0,
    "knownRenewals": 0,
    "totalProjected": 90000,
    "isEstimate": true
  },
  "upcomingCertificates": [],
  "lastUpdatedAt": "2026-09-28T12:00:00Z"
}
```

Siempre distinguir `caused` de `projected` y mostrar fecha de actualización.

## 15. Mensajes de usuario

Los textos deben vivir en recursos o catálogo de mensajes, no dispersos en controladores.

| Código | Mensaje |
|---|---|
| `certificate.request.created` | `Solicitud creada. Te avisaremos cuando haya novedades.` |
| `certificate.kyc.required` | `Completa la verificación de identidad para continuar.` |
| `certificate.documents.required` | `Viafirma solicita documentos adicionales para continuar.` |
| `certificate.provider.review` | `La solicitud está en revisión. No necesitas hacer nada por ahora.` |
| `certificate.ready` | `El certificado fue emitido y está listo para instalarse.` |
| `certificate.active` | `El certificado está activo y listo para firmar documentos.` |
| `certificate.renewal.scheduled` | `La renovación automática está programada.` |
| `certificate.renewal.in_progress` | `La renovación está en proceso. El certificado actual seguirá activo.` |
| `certificate.expiring_soon` | `El certificado vence pronto. Revisa el estado de la renovación.` |
| `certificate.renewal.failed` | `No pudimos completar la renovación automática. Revisa la acción pendiente.` |
| `certificate.expired` | `El certificado venció y no puede usarse para emitir documentos.` |
| `certificate.invalid_p12` | `El certificado recibido no pudo validarse. El equipo revisará la solicitud.` |
| `certificate.rejected` | `La solicitud no fue aprobada. Revisa el motivo e inicia una nueva solicitud si es necesario.` |
| `certificate.revoked` | `El certificado fue revocado y ya no puede utilizarse.` |
| `certificate.price_unavailable` | `No hay un precio vigente para este tipo de certificado. Contacta al administrador.` |

No mostrar al usuario `status_invalid_for_this_action`, `request_not_found`, `client_not_found` ni excepciones del proveedor.

## 16. Logs y observabilidad

### 16.1 Campos comunes

Todo log debe incluir, si existe: `TimestampUtc`, `Level`, `Service`, `Environment`, `CorrelationId`, `OperationId`, `TenantId`, `ClientId`, `CertificateRequestId`, `CertificateId`, `ProviderKey`, `ProviderEnvironment`, `Attempt`, `DurationMs`, `Outcome` y `ErrorCode`.

### 16.2 Logs de información

```text
CertificateRequestCreated RequestId={RequestId} TenantId={TenantId} ClientId={ClientId} Type={RequestType} Profile={ProfileKey} Environment={Environment}
CertificateProviderStatusChanged RequestId={RequestId} Previous={PreviousStatus} Current={CurrentStatus} External={ExternalStatus}
CertificateKycLinkGenerated RequestId={RequestId} LinkExpiresAt={LinkExpiresAt}
CertificateP7bDownloaded RequestId={RequestId} PublicKeyHash={PublicKeyHash} Bytes={Bytes}
CertificateInstalled CertificateId={CertificateId} NotAfter={NotAfter} Thumbprint={ThumbprintMasked}
CertificateActivated ClientId={ClientId} PreviousCertificateId={PreviousCertificateId} NewCertificateId={NewCertificateId}
CertificateChargeCreated ChargeId={ChargeId} Amount={Amount} Currency={Currency} Trigger={Trigger}
CertificateRenewalScheduled CertificateId={CertificateId} RenewalAt={RenewalAt}
```

### 16.3 Logs de error

```text
CertificateProviderCallFailed Operation={Operation} HttpStatus={HttpStatus} ProviderCode={ProviderCode} Retryable={Retryable}
CertificateProviderAuthenticationFailed Provider={ProviderKey} Environment={Environment}
CertificateRequestSyncFailed RequestId={RequestId} ExternalStatus={ExternalStatus} ErrorCode={ErrorCode} RetryAt={RetryAt}
CertificateP7bValidationFailed RequestId={RequestId} Reason={Reason}
CertificateKeyPairMismatch RequestId={RequestId} CsrHash={CsrHash} P7bPublicKeyHash={P7bPublicKeyHash}
CertificateActivationSkipped ClientId={ClientId} Reason={Reason}
CertificateChargeDuplicateIgnored RequestId={RequestId} ExistingChargeId={ExistingChargeId}
```

Nunca registrar consumer secret, password, clave privada, CSR completo, P12, P7B en base64 ni token KYC completo.

### 16.4 Métricas

- `certificates.requests.created.total`.
- `certificates.requests.by_status`.
- `certificates.provider_calls.total`.
- `certificates.provider_calls.failed`.
- `certificates.provider_call_duration_ms`.
- `certificates.kyc.pending`.
- `certificates.documents_required`.
- `certificates.p7b.downloaded`.
- `certificates.installation.failed`.
- `certificates.renewals.started`.
- `certificates.renewals.completed`.
- `certificates.renewals.at_risk`.
- `certificates.expiring.days_remaining`.
- `certificate_charges.accrued.total`.
- `certificate_charges.duplicates_prevented`.

## 17. Errores y recuperación

| Categoría | Ejemplo | Acción |
|---|---|---|
| Validación | Campo requerido o regex inválida | Mostrar al usuario, no reintentar. |
| Autenticación | 401 o 403 | Pausar proveedor y alertar Superadmin. |
| No encontrado | `request_not_found` | Marcar inconsistencia, no crear otra solicitud. |
| Estado inválido | `status_invalid_for_this_action` | Sin reintento ciego; sincronizar primero. |
| Red | Timeout, 408, 429, 5xx | Backoff y límite de intentos. |
| KYC | `NOT_MATCHING`, `REJECTED` | Acción del usuario o soporte. |
| Documento | `file_not_uploaded` | Reintento manual y auditoría. |
| Criptografía | P7B no coincide o P12 inválido | No activar, aislar material y alertar. |
| Billing | Precio inexistente | No enviar solicitud cobrable. |
| Datos | NIT o sujeto no coincide | No activar, marcar revisión. |

## 18. Seguridad y cumplimiento

- Permisos separados: `Certificate.Read`, `Certificate.Request`, `Certificate.Renew`, `Certificate.Revoke`, `Certificate.PriceAdmin` y `Certificate.SupportRetry`.
- Autorización por tenant en cada consulta y comando.
- Cifrado en reposo y tránsito.
- Secretos en vault y rotación sin editar código.
- Evidencia de aceptación de términos y condiciones.
- URL y hash de términos vigentes, consultados periódicamente desde el perfil.
- Retención de eventos y archivos según política legal.
- Nunca exponer P12 ni passwords por endpoints públicos.
- Backend valida siempre tenant, cliente, perfil y precio.
- Verificar NIT y sujeto antes de activar.

## 19. Pruebas requeridas

### Unitarias

- Mapeo de estados Viafirma.
- Ventana de renovación con UTC y zona horaria.
- Selección de precio y prevención de vigencias superpuestas.
- Idempotencia de cargos, descarga e instalación.
- Pareja CSR, clave privada y P7B.
- Un solo certificado activo por cliente y ambiente.
- Clasificación de errores reintentables.

### Integración con Viafirma

- Perfiles Sandbox y Producción.
- FE-PJ y FE-PN.
- Estado básico y avanzado.
- KYC en estados permitidos.
- Anexos y listado de archivos.
- P7B en todos los estados compatibles.
- Rechazo permitido y no permitido.
- Revocación válida e inválida.
- 401, 403, 404, 429 y 5xx.

### Integración interna y operación

- Emitir con certificado activo.
- Renovar sin interrumpir emisión concurrente.
- Fallar instalación y conservar certificado anterior.
- Doble worker sin doble cargo.
- Aislamiento entre tenants.
- Dashboard con causado y proyectado.
- Reiniciar worker durante cada etapa.
- Reprocesar después de timeout.
- Proveedor no disponible varios días.
- Rollover controlado.
- Logs sin secretos.

## Estado de implementación al 2026-09-28

Implementado en código:

- Modelo de ciclo de vida para proveedores, perfiles, solicitudes, precios, cargos y eventos.
- Integración Viafirma PKCS#10 con OAuth 1.0, ambientes separados, perfiles, solicitudes, estados, KYC, archivos, descarga P7B y revocación.
- Factoría de contexto que obtiene URLs y secretos desde configuración segura.
- Endpoint Superadmin para sincronizar perfiles y campos del proveedor.
- Generación de CSR RSA configurable, hash de clave pública y protección de la clave privada mediante `ICryptoVault`.
- Diseño de entrega segura del P12 al cliente con password de 16 caracteres y auditoría.
- Registro de servicios en Tenant, Client, Superadmin y Worker.

### Principio operativo confirmado

La generación y operación del certificado será server-side y automática. El tenant no debe cargar ni generar manualmente el CSR, la clave privada, el P7B o el P12. Nuestro backend generará la pareja criptográfica, construirá el CSR, enviará la solicitud, sincronizará estados, atenderá reintentos, descargará el P7B, ensamblará el P12 y preparará su entrega al cliente.

Sandbox podrá mostrar información técnica para depuración, pero únicamente de forma controlada: estados, códigos externos, hashes, tiempos, códigos HTTP y mensajes sanitizados. Nunca se mostrarán ni registrarán la clave privada, CSR completo, P7B en Base64, P12, passwords, consumer secret o tokens completos.

La única interacción humana potencial será la que Viafirma exija al cliente final para KYC, aceptación de términos o entrega de documentos. FacilFactura debe orquestar esa interacción por correo y continuar automáticamente cuando el proveedor reporte que fue completada.

Pendiente inmediato:

- La migración explícita `20260928193000_AddCertificateLifecycle` fue agregada, compilada, reconocida por EF y aplicada correctamente en la base local de pruebas y en producción. Producción quedó sin migraciones pendientes.
- Se agregó la migración incremental `20260928210000_AddEncryptedCertificateProviderCredentials`, que permite guardar credenciales Sandbox y Producción cifradas por ambiente para configurarlas desde Superadmin.
- Se agregó la pantalla Admin de configuración del proveedor, con prueba de conexión Sandbox y sin devolver credenciales almacenadas.
- Implementar el caso de uso de creación de solicitud desde Tenant.
- Completar ensamblado, instalación y exportación del P12.
- Agregar worker de sincronización, reintentos y renovación automática.

## 20. Criterios de aceptación

1. El tenant inicia una solicitud sin conocer códigos internos de Viafirma.
2. Perfiles y códigos se sincronizan por ambiente.
3. La aceptación de términos queda demostrable.
4. El estado se refleja automáticamente en el portal.
5. KYC y documentos pendientes muestran una acción clara.
6. Una solicitud aprobada produce P12 verificable.
7. No se activa P12 que no coincida con CSR o NIT.
8. La renovación comienza dentro de la ventana configurada.
9. El certificado actual sigue activo hasta validar el nuevo.
10. No existen dos certificados activos por cliente y ambiente.
11. Reintentos no duplican solicitudes, certificados ni cargos.
12. Superadmin administra precios sin recompilar.
13. Cada cargo conserva el precio histórico.
14. Tenant consulta cargos actuales y proyección siguiente.
15. Se alerta una renovación en riesgo.
16. UI no muestra códigos técnicos.
17. Logs permiten trazar una solicitud sin secretos.
18. Sandbox y Producción funcionan por configuración.
19. Controladores no están acoplados a Viafirma.
20. Una caída del proveedor no desactiva certificados ya activos.

## 21. Plan por fases

### Fase 0 - Decisiones

- Documentar la autorización de Viafirma para custodia server-side y generación de CSR.
- Confirmar en certificación de integración el comportamiento de KYC recurrente, renovación, términos y perfiles.
- Definir cargo, reembolso, retención y fallback ante vencimiento.

### Fase 1 - Fundaciones

- Entidades, enums, migraciones y configuración.
- Vault references.
- `ICertificateProvider` y cliente OAuth.
- Auditoría, idempotencia y locks.
- Sincronización de perfiles.

### Fase 2 - Solicitud

- Generación de material y CSR.
- Formulario dinámico.
- Términos, solicitud, KYC y anexos.

### Fase 3 - Instalación

- Estados, descarga P7B, ensamblaje P12 y validación.
- Activación inicial.
- Integración con el worker de firma.

### Fase 4 - Renovación

- Programación, certificado futuro, rollover, revocación y alertas.

### Fase 5 - Billing y dashboard

- Precios en Superadmin.
- Cargos versionados y corte mensual.
- Consumo actual y proyección tenant.

### Fase 6 - Productivo

- Sandbox completo.
- Recuperación y doble ejecución.
- Renovación controlada.
- Seguridad y despliegue gradual por tenant.

## 22. Riesgos y decisiones pendientes

| Riesgo o decisión | Impacto | Responsable |
|---|---|---|
| La custodia server-side autorizada no cumple el control de seguridad interno | Renovación totalmente automática bloqueada hasta corregir controles | Seguridad, arquitectura y producto |
| KYC obligatorio en cada renovación | Requiere representante | Producto y Viafirma |
| Cambio de precio del proveedor | Margen negativo | Administración |
| P7B no coincide con CSR | No se puede activar | Ingeniería y proveedor |
| Renovación incompleta al vencimiento | Interrupción de emisión | Producto |
| Perfiles distintos por ambiente | Solicitudes inválidas | Integración |
| Cargo antes de aprobación | Cobro indebido | Finanzas |
| Cambio de NIT durante renovación | Certificado incorrecto | Dominio |

## 23. Recomendación final

Construir esto como un módulo de ciclo de vida, no como una ampliación del endpoint actual de carga manual. La carga manual puede mantenerse como contingencia, pero debe reutilizar validación, almacenamiento, activación, auditoría y billing.

Con la autorización de Viafirma para generar y custodiar la clave privada, este diseño permite implementar una operación idempotente, observable, administrable, multiambiente y preparada para múltiples proveedores. Antes de producción queda la certificación técnica del flujo completo y la validación de controles internos.

## 24. Incidencia de sincronización de perfiles - 2026-09-28

La prueba de conexión a Sandbox confirmó autenticación, URL y disponibilidad de dos perfiles. La sincronización falló posteriormente al consultar los campos de un perfil: Viafirma respondió HTTP 200 con una página HTML de su aplicación RA, no con JSON. El navegador mostró CORS porque la excepción no controlada generaba una respuesta 500 sin las cabeceras de CORS; CORS no era la causa original.

Se incorporó:

- Fallback para consultar el código de perfil decodificado cuando la variante Base64 entregada por `available-profiles` devuelve HTML.
- Respuesta JSON controlada `502` con código funcional y log estructurado cuando el proveedor devuelve una respuesta inválida.
- Despliegue directo de `fel-api-superadmin` en producción mediante `scripts/deploy.ps1`; el migrador terminó correctamente y no se aplicaron migraciones nuevas.

Durante la primera sincronización completa se confirmó que Viafirma entregaba correctamente los perfiles y sus campos, pero el catálogo de países excedía el límite inicial de 1000 caracteres de `DefaultValue`. La migración `20260928220000_IncreaseCertificateProfileFieldDefaultValue` amplió el límite a 4000 caracteres y fue aplicada en producción por `fel-migrator`. La verificación final es repetir `Sincronizar perfiles` desde Admin y confirmar que ambos perfiles quedan almacenados con sus campos. No se deben registrar credenciales ni respuestas completas del proveedor en logs.
