# Facil Factura - Deploy Script (Windows)
# Transfers code via SCP and rebuilds Docker containers on the server
# Usage: .\scripts\deploy.ps1 [-Server 137.184.208.78] [-Key ~\.ssh\deploy_facil_factura]

param(
    [string]$Server = "137.184.208.78",
    [string]$Key = "$env:USERPROFILE\.ssh\deploy_facil_factura",
    [switch]$SkipBuild,
    [switch]$Pull,
    [string]$Service = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$RemoteDir = "/opt/FacilFactura"
# -Pull: baja de nuevo las imágenes base (parches del runtime) en vez de usar las que ya están en el servidor
$PullFlag = if ($Pull) { "--pull " } else { "" }
$TempTar = Join-Path $env:TEMP "facilfactura.tar.gz"

# Espera a que el droplet tenga suficiente RAM libre antes de arrancar un build de Docker —
# un `dotnet publish` o `npm run build` bajo poca memoria es lo que llevó al OOM killer a matar
# sqlservr en producción (ver historial). No aborta el deploy si sigue bajo tras los reintentos,
# solo avisa: en la práctica el build igual puede completar usando swap, más lento pero sin OOM.
function Wait-ForFreeMemory {
    param([string]$Server, [string]$Key, [int]$MinMB = 500, [int]$MaxAttempts = 4)
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        $available = & ssh -o BatchMode=yes -i $Key "root@${Server}" "free -m | awk '/^Mem:/{print `$7}'"
        $availableInt = 0
        [void][int]::TryParse(($available -join '').Trim(), [ref]$availableInt)
        if ($availableInt -ge $MinMB) { return }
        Write-Host "  Memoria disponible baja (${availableInt}MB) - esperando 15s antes de construir..."
        Start-Sleep 15
    }
    Write-Host "  Advertencia: memoria disponible sigue baja tras $MaxAttempts intentos; se continua de todas formas."
}

# Confirma que el contenedor efectivamente quedó corriendo tras `docker compose up -d` — si un
# build se completó pero el contenedor se cayó al arrancar (crash-loop, puerto ocupado, etc.),
# es mejor frenar el deploy ahí mismo que seguir con el resto y descubrirlo al final.
function Assert-ContainerRunning {
    param([string]$Server, [string]$Key, [string]$ContainerName)
    $status = & ssh -o BatchMode=yes -i $Key "root@${Server}" "docker ps --filter name=^/${ContainerName}`$ --filter status=running --format '{{.Names}}'"
    if (-not ($status -join '').Trim()) {
        throw "El contenedor '$ContainerName' no quedo corriendo despues del deploy."
    }
}

# Nombre del servicio de docker-compose -> nombre real del contenedor (container_name en
# docker-compose.yml) — no siempre coinciden (ej. servicio "client-web" -> contenedor
# "fel-client-web"). fel-migrator es un job de un solo uso (corre las migraciones y termina),
# nunca queda "Up", así que se excluye del chequeo de salud, no de la lista de build.
$ServiceToContainer = @{
    "fel-migrator"        = "fel-migrator"
    "fel-api-tenant"      = "fel-api-tenant"
    "fel-api-integration" = "fel-api-integration"
    "fel-api-superadmin"  = "fel-api-superadmin"
    "fel-api-client"      = "fel-api-client"
    "fel-worker"          = "fel-worker"
    "landing-web"         = "fel-landing-web"
    "tenant-web"          = "fel-tenant-web"
    "superadmin-web"      = "fel-superadmin-web"
    "client-web"          = "fel-client-web"
    "developers-web"      = "fel-developers-web"
}

Write-Host "=========================================="
Write-Host "  Facil Factura - Deploy (SCP)"
Write-Host "=========================================="

if (-not (Test-Path $Key)) {
    throw "SSH key not found: $Key"
}

# 1. Verify SSH connection (con reintentos: el droplet a veces tarda en responder el
# handshake bajo carga puntual — un solo intento fallido no debe abortar el deploy entero).
Write-Host "[1/5] Verifying SSH connection..."
$sshOk = $false
for ($attempt = 1; $attempt -le 3; $attempt++) {
    & ssh -o BatchMode=yes -o ConnectTimeout=10 -i $Key "root@${Server}" "echo SSH-OK"
    if ($LASTEXITCODE -eq 0) { $sshOk = $true; break }
    if ($attempt -lt 3) {
        Write-Host "  Intento $attempt fallido, reintentando en 5s..."
        Start-Sleep 5
    }
}
if (-not $sshOk) {
    throw "SSH connection failed after 3 attempts"
}

# 2. Paquete del commit actual (HEAD), no del directorio de trabajo: lo que no está commiteado no se
# despliega. Antes se empaquetaba la carpeta tal cual y subían a producción cambios a medio hacer de
# otra sesión o rama. git archive solo trae archivos versionados (nada de bin, obj, node_modules ni .env).
Write-Host "[2/5] Creating tarball from HEAD..."
Push-Location $ProjectRoot
try {
    $head = (& git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed" }
    $pending = & git status --porcelain
    if ($pending) {
        Write-Host "  Advertencia: hay $(@($pending).Count) cambio(s) sin commitear; NO se despliegan (se despliega $head)."
    }
    & git archive --format=tar.gz -o $TempTar HEAD -- . ":(exclude)*.pdf" ":(exclude)*.zip"
    if ($LASTEXITCODE -ne 0) { throw "git archive failed" }
    Write-Host "  Commit desplegado: $head"
} finally {
    Pop-Location
}

# 3. Transfer and extract on server
Write-Host "[3/5] Transferring to ${Server}:$RemoteDir ..."
& scp -o BatchMode=yes -i $Key $TempTar "root@${Server}:/tmp/facilfactura.tar.gz"
if ($LASTEXITCODE -ne 0) { throw "scp failed" }

# Se borran src/ y apps/ antes de extraer: tar nunca elimina, así que archivos borrados o renombrados en el
# repo se quedaban en el servidor y se seguían compilando (pasó con dos migraciones viejas). Ningún
# contenedor monta esas carpetas; .env, deploy/ (montajes de nginx) y los volúmenes no se tocan.
& ssh -o BatchMode=yes -i $Key "root@${Server}" "mkdir -p $RemoteDir && rm -rf $RemoteDir/src $RemoteDir/apps && tar -xzf /tmp/facilfactura.tar.gz -C $RemoteDir && rm -f /tmp/facilfactura.tar.gz"
if ($LASTEXITCODE -ne 0) { throw "extract failed" }

# Los servicios .NET dependen del migrador en docker-compose (service_completed_successfully).
$DotnetServices = @("fel-api-tenant", "fel-api-integration", "fel-api-superadmin", "fel-api-client", "fel-worker")

# Reconstruye el migrador con el código recién extraído y lo corre hasta que termine. Siempre se
# reconstruye: una imagen vieja cuyo modelo no coincide con sus migraciones falla con
# PendingModelChangesWarning y, como dependencia, dejaba detenido el servicio que se estaba desplegando.
# Si falla, el despliegue se corta aquí y los contenedores que estaban corriendo siguen igual.
function Invoke-Migrator {
    param([string]$Server, [string]$Key)
    Write-Host "  -> fel-migrator (build + migraciones)"
    Wait-ForFreeMemory -Server $Server -Key $Key
    & ssh -o BatchMode=yes -i $Key "root@${Server}" "cd $RemoteDir && docker compose build $PullFlag fel-migrator 2>&1 && docker compose up --no-deps --exit-code-from fel-migrator fel-migrator 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "El migrador fallo; no se reinicio ningun servicio. Revisa: docker logs fel-migrator" }
}

# Construye un servicio y solo después lo recrea, sin dependencias: el migrador ya corrió (o el servicio no lo
# necesita), y sin --no-deps compose volvería a correrlo y, si fallara, dejaría el servicio detenido.
function Deploy-ServiceContainer {
    param([string]$Server, [string]$Key, [string]$Svc)
    Wait-ForFreeMemory -Server $Server -Key $Key
    # 2>&1 en el shell remoto: docker compose escribe el progreso por stderr y
    # PowerShell 5.1 lo convierte en error terminante con ErrorActionPreference=Stop.
    & ssh -o BatchMode=yes -i $Key "root@${Server}" "cd $RemoteDir && docker compose build $PullFlag$Svc 2>&1 && docker compose up -d --no-deps $Svc 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "docker build/up failed for service '$Svc'" }
    if ($ServiceToContainer.ContainsKey($Svc)) {
        Assert-ContainerRunning -Server $Server -Key $Key -ContainerName $ServiceToContainer[$Svc]
    }
}

# 4. Build and start containers
if ($SkipBuild) {
    Write-Host "[4/5] Skipping Docker build (SkipBuild)."
} elseif ($Service) {
    Write-Host "[4/5] Building and starting only '$Service'..."
    if ($Service -eq "fel-migrator" -or $DotnetServices -contains $Service) {
        Invoke-Migrator -Server $Server -Key $Key
    }
    if ($Service -ne "fel-migrator") {
        Deploy-ServiceContainer -Server $Server -Key $Key -Svc $Service
    }
} else {
    # Uno a la vez, nunca "docker compose build" a secas: ese build en paralelo de los ~10
    # servicios (5 publish de .NET + 5 build de Vite concurrentes) agotó la RAM del droplet
    # (3.8GB) y el OOM killer del sistema mató el proceso de sqlservr dos veces en producción
    # (ver incidente documentado). Reconstruir cada servicio por separado mantiene el pico de
    # memoria acotado al de un solo build, igual que -Service, pero recorriendo todos.
    # El migrador va primero y aparte; los demás se levantan sin dependencias (ver Deploy-ServiceContainer).
    $BuildableServices = $DotnetServices + @("landing-web", "tenant-web", "superadmin-web", "client-web", "developers-web")
    Write-Host "[4/5] Building and starting containers one at a time (migrador + $($BuildableServices.Count) services)..."
    Invoke-Migrator -Server $Server -Key $Key
    foreach ($svc in $BuildableServices) {
        Write-Host "  -> $svc"
        Deploy-ServiceContainer -Server $Server -Key $Key -Svc $svc
    }
}

# 4b. Aplicar configuracion de nginx
#
# Hace falta RECREAR, no basta con recargar. docker-compose monta nginx.conf como bind
# mount de un fichero suelto, y ese tipo de montaje fija el inodo al crear el contenedor.
# tar recrea el fichero con inodo nuevo al desplegar, asi que el contenedor sigue viendo
# el viejo: un reload releeria la version anterior. El directorio deploy/sites si se
# actualiza en vivo por ser montaje de directorio, lo que deja ambos lados descuadrados.
#
# Se valida antes en un contenedor desechable, porque `nginx -t` dentro del que esta
# corriendo probaria la configuracion vieja por el mismo motivo. Una config invalida
# dejaria caido el proxy entero.
#
# Con -Service (despliegue dirigido a un solo servicio) no aplica: nginx no cambio, y
# esperar su validacion/recreate + el barrido de endpoints solo alarga una iteracion
# que se supone rapida.
if (-not $Service) {
Write-Host "[4b/5] Validating nginx config..."
$validate = "docker run --rm -v ${RemoteDir}/deploy/nginx.conf:/etc/nginx/nginx.conf:ro " +
            "-v ${RemoteDir}/deploy/sites:/etc/nginx/conf.d:ro " +
            # 2>&1 lo ejecuta el shell remoto: nginx -t escribe siempre por stderr y
            # PowerShell 5.1 convierte el stderr de un nativo en error terminante.
            "-v facilfactura_certbot_conf:/etc/letsencrypt:ro nginx:alpine nginx -t 2>&1"
& ssh -o BatchMode=yes -i $Key "root@${Server}" $validate
if ($LASTEXITCODE -ne 0) { throw "nginx config invalida - despliegue detenido antes de aplicarla" }

Write-Host "[4b/5] Recreating nginx..."
& ssh -o BatchMode=yes -i $Key "root@${Server}" "cd $RemoteDir && docker compose up -d --force-recreate nginx 2>&1"
if ($LASTEXITCODE -ne 0) { throw "nginx recreate failed" }

# 5. Verify endpoints
Write-Host "[5/5] Verifying endpoints..."
Start-Sleep 10
foreach ($sub in @("facil-factura.pro", "api", "tenants", "clients", "admin", "developers")) {
    $hostname = if ($sub -eq "facil-factura.pro") { "facil-factura.pro" } else { "$sub.facil-factura.pro" }
    try {
        $r = Invoke-WebRequest -Uri "https://$hostname" -UseBasicParsing -TimeoutSec 20
        Write-Host "  https://$hostname -> HTTP $($r.StatusCode)"
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "  https://$hostname -> HTTP $code"
    }
}
}

Remove-Item $TempTar -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=========================================="
Write-Host "  Deploy Complete!"
Write-Host "=========================================="
Write-Host ""
Write-Host "Apps:"
Write-Host "  Landing:    https://facil-factura.pro"
Write-Host "  Tenants:    https://tenants.facil-factura.pro"
Write-Host "  Clients:    https://clients.facil-factura.pro"
Write-Host "  Admin:      https://admin.facil-factura.pro"
Write-Host "  Developers: https://developers.facil-factura.pro"
Write-Host "  API:        https://api.facil-factura.pro"
