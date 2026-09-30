# Plan de seguridad y hacking ético

## Recomendación

La mejor opción evaluada para este proyecto es la skill **Deliberate – pentest**:

<https://github.com/angad-kandhari/deliberate/tree/main/skills/pentest>

Está enfocada en pruebas de penetración autorizadas sobre código y sistemas propios. Su flujo contempla:

- Confirmar autorización y alcance.
- Mapear superficie de ataque y límites de confianza.
- Revisar autenticación, autorización, inyección, secretos y lógica de negocio.
- Reproducir los hallazgos con pruebas mínimas y controladas.
- Documentar impacto, severidad y corrección.
- Verificar que la vulnerabilidad quede cerrada y agregar regresión.

## Cómo aplicarla a FacilFactura

El primer alcance recomendado es una auditoría defensiva del portal de tenants y su API:

1. Aislamiento entre tenants y validación de `x-tenant-id`/JWT.
2. Endpoints de clientes, RUT y certificados.
3. Flujo Sandbox de Viafirma y manipulación de `formValues`.
4. CORS, sesiones, expiración de tokens y cierre de sesión.
5. Secretos, claves de Google Maps, URLs y valores de configuración hardcodeados.
6. Cargas de archivos PDF y validación de contenido.
7. Errores 401/403/500 y exposición de información sensible.
8. Dependencias, imágenes Docker y configuración de despliegue.

## Skills complementarias

Si están disponibles en el mismo proyecto, se pueden usar también:

- `secure`: aplicar correcciones seguras a cada hallazgo.
- `verify`: repetir la prueba original después de corregir.
- Una skill de revisión de supply chain o dependencias para paquetes, imágenes Docker y SBOM.

## Opciones descartadas como primera elección

- `codex-security/validation`: valida hallazgos existentes; no es el análisis inicial completo.
- `agent-skills-collection`: contiene muchas skills de seguridad, pero es una colección amplia y menos enfocada.
- `agentic-security-scanner`: está orientada principalmente a revisar agentes y skills, no la aplicación FacilFactura.

## Reglas de uso

- Probar únicamente el código, infraestructura y dominios bajo control de FacilFactura.
- Preferir Sandbox, datos sintéticos y pruebas no destructivas.
- No ejecutar denegación de servicio, extracción masiva, persistencia ni pruebas destructivas.
- No incluir credenciales reales ni datos de clientes en reportes externos.
- Revisar el contenido de la skill y sus scripts antes de instalarla.
- Cada hallazgo debe incluir ubicación, precondiciones, reproducción, impacto, severidad, corrección y prueba de cierre.

