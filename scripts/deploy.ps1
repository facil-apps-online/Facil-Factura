# Facil Factura - Deploy Script (Windows)
# Transfers code via SCP and rebuilds Docker containers on the server
# Usage: .\scripts\deploy.ps1 [-Server 137.184.208.78] [-Key ~\.ssh\deploy_facil_factura]

param(
    [string]$Server = "137.184.208.78",
    [string]$Key = "$env:USERPROFILE\.ssh\deploy_facil_factura",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$RemoteDir = "/opt/FacilFactura"
$TempTar = Join-Path $env:TEMP "facilfactura.tar.gz"

Write-Host "=========================================="
Write-Host "  Facil Factura - Deploy (SCP)"
Write-Host "=========================================="

if (-not (Test-Path $Key)) {
    throw "SSH key not found: $Key"
}

# 1. Verify SSH connection
Write-Host "[1/5] Verifying SSH connection..."
& ssh -o BatchMode=yes -o ConnectTimeout=10 -i $Key "root@${Server}" "echo SSH-OK"
if ($LASTEXITCODE -ne 0) {
    throw "SSH connection failed"
}

# 2. Create tarball excluding git, bin, obj, .env, node_modules
Write-Host "[2/5] Creating tarball..."
Push-Location $ProjectRoot
try {
    & tar -czf $TempTar `
        --exclude=".git" `
        --exclude="bin" `
        --exclude="obj" `
        --exclude=".env" `
        --exclude=".env.*" `
        --exclude="node_modules" `
        --exclude="scratch" `
        --exclude="Validador_rips_ips" `
        --exclude="*.pdf" `
        --exclude="*.zip" `
        .
    if ($LASTEXITCODE -ne 0) { throw "tar failed" }
} finally {
    Pop-Location
}

# 3. Transfer and extract on server
Write-Host "[3/5] Transferring to ${Server}:$RemoteDir ..."
& scp -o BatchMode=yes -i $Key $TempTar "root@${Server}:/tmp/facilfactura.tar.gz"
if ($LASTEXITCODE -ne 0) { throw "scp failed" }

& ssh -o BatchMode=yes -i $Key "root@${Server}" "mkdir -p $RemoteDir && tar -xzf /tmp/facilfactura.tar.gz -C $RemoteDir && rm -f /tmp/facilfactura.tar.gz"
if ($LASTEXITCODE -ne 0) { throw "extract failed" }

# 4. Build and start containers
if ($SkipBuild) {
    Write-Host "[4/5] Skipping Docker build (SkipBuild)."
} else {
    Write-Host "[4/5] Building Docker images and starting containers..."
    & ssh -o BatchMode=yes -i $Key "root@${Server}" "cd $RemoteDir && docker compose build && docker compose up -d"
    if ($LASTEXITCODE -ne 0) { throw "docker build/up failed" }
}

# 5. Verify endpoints
Write-Host "[5/5] Verifying endpoints..."
Start-Sleep 10
foreach ($sub in @("facil-factura.pro", "api", "tenants", "clients", "admin")) {
    $hostname = if ($sub -eq "facil-factura.pro") { "facil-factura.pro" } else { "$sub.facil-factura.pro" }
    try {
        $r = Invoke-WebRequest -Uri "https://$hostname" -UseBasicParsing -TimeoutSec 20
        Write-Host "  https://$hostname -> HTTP $($r.StatusCode)"
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "  https://$hostname -> HTTP $code"
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
Write-Host "  API:        https://api.facil-factura.pro"
