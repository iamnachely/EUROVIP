# ============================================================
# Deploy.ps1 - Blue-Green Deployment para EUROVIP (BackOffice) en IIS
#
# Copiado de BackTiTan (DeploymentScripts/Deploy.ps1) y adaptado a Eurovip:
#   - se preservan wwwroot\imagenes, wwwroot\uploads y Logs (fotos y archivos subidos)
#   - la configuración real vive en appsettings.Local.json (no está en el repo)
#
# Detecta el slot activo directamente desde IIS (sin marker files).
# Despliega al slot inactivo, hace el switch, verifica con health check.
# Rollback automático si el health check falla.
# La configuración del servidor (appsettings.json) se preserva en memoria,
# no se crean archivos de backup en disco.
#
# Prerrequisitos en el servidor:
#   - IIS con sitio "eurovip" apuntando a Blue o Green
#   - Carpetas: C:\inetpub\wwwroot\eurovip\Blue
#               C:\inetpub\wwwroot\eurovip\Green
#   - appsettings.Local.json (cadena de conexión, WhatsApp…) copiado a mano en Blue y Green
#   - .NET 8 Hosting Bundle instalado (ASP.NET Core Module V2)
#   - Módulo WebAdministration de PowerShell disponible
#   - PowerShell 5.1+ (preferible 7+ para HTTPS con cert self-signed)
#   - El sitio debe tener el endpoint /health respondiendo 200
#     (Eurovip: Program.cs + Services/BaseDatosHealthCheck.cs; comprueba también la base)
# ============================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectName,

    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath,

    [Parameter(Mandatory = $false)]
    [string]$HealthCheckPath = "/health",

    [Parameter(Mandatory = $false)]
    [int]$HealthCheckRetries = 12,

    [Parameter(Mandatory = $false)]
    [int]$HealthCheckWaitSeconds = 10,

    # Eurovip: solo para el PRIMER deploy (si el sitio IIS no existe, se crea con este puerto/host)
    [Parameter(Mandatory = $false)]
    [int]$Puerto = 8090,

    [Parameter(Mandatory = $false)]
    [string]$NombreHost = ""
)

# Eurovip: la configuración real (appsettings.Local.json) llega del pipeline como variable SECRETA
# (EurovipAppSettingsLocal, el JSON en base64) en la variable de entorno EUROVIP_APPSETTINGS_LOCAL.
# Si viene, se escribe en el slot; si no, se conserva la que ya tenga el servidor.
$appSettingsLocalB64 = $env:EUROVIP_APPSETTINGS_LOCAL

$ErrorActionPreference = "Stop"

$siteName    = $ProjectName
$basePath    = "C:\inetpub\wwwroot"
$projectRoot = "$basePath\$ProjectName"
$bluePath    = "$projectRoot\Blue"
$greenPath   = "$projectRoot\Green"

# ============================================================
# HELPERS
# ============================================================
function Write-Step  { param([string]$m) Write-Host "`n>>> $m" -ForegroundColor Yellow }
function Write-OK    { param([string]$m) Write-Host "    [OK] $m" -ForegroundColor Green }
function Write-Warn  { param([string]$m) Write-Host "    [WARN] $m" -ForegroundColor DarkYellow }
function Write-Fail  { param([string]$m) Write-Host "    [FAIL] $m" -ForegroundColor Red }
function Write-Banner {
    param([string]$m)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  $m" -ForegroundColor White
    Write-Host "============================================================" -ForegroundColor Cyan
}

# Detiene el App Pool usando appcmd.exe con reintentos.
# Se usa appcmd.exe en lugar de Stop-WebAppPool porque el error 0x80070425
# afecta a TODAS las operaciones del módulo WebAdministration (incluyendo
# Get-WebAppPoolState y Stop-WebAppPool) cuando WAS está ocupado.
# appcmd.exe usa una comunicación diferente con IIS y es más resistente.
function Stop-AppPoolSafe {
    param([string]$Name)

    $appcmd = Join-Path $env:SystemRoot "System32\inetsrv\appcmd.exe"

    # Leer estado con reintentos (Get-WebAppPoolState también puede lanzar 0x80070425)
    $state = "Unknown"
    for ($i = 1; $i -le 5; $i++) {
        try {
            $state = (Get-WebAppPoolState -Name $Name).Value
            break
        } catch {
            Write-Host "    GetState intento $i/5, reintentando en 3s..." -ForegroundColor Gray
            Start-Sleep -Seconds 3
        }
    }

    # Esperar estado estable si está en transición
    $waited = 0
    while ($state -in @("Starting", "Stopping") -and $waited -lt 30) {
        Write-Host "    App Pool '$state', esperando..." -ForegroundColor Gray
        Start-Sleep -Seconds 3
        $waited += 3
        try { $state = (Get-WebAppPoolState -Name $Name).Value } catch { }
    }

    if ($state -eq "Stopped") { return }  # Ya estaba detenido, nada que hacer

    # Detener con appcmd.exe + reintentos
    for ($i = 1; $i -le 5; $i++) {
        try {
            & $appcmd stop apppool /apppool.name:"$Name" 2>&1 | Out-Null
            Start-Sleep -Seconds 2
            Write-Host "    App Pool detenido." -ForegroundColor Gray
            return
        } catch {
            if ($i -lt 5) {
                Write-Host "    Stop intento $i/5, reintentando en 3s..." -ForegroundColor Gray
                Start-Sleep -Seconds 3
            } else {
                throw "No se pudo detener el App Pool '$Name' tras 5 intentos: $($_.Exception.Message)"
            }
        }
    }
}

# ============================================================
# 1. VALIDACIONES INICIALES
# ============================================================
Write-Banner "DEPLOY BLUE-GREEN - $ProjectName"
Write-Host "  Build: $env:BUILD_BUILDNUMBER" -ForegroundColor Gray
$shortCommit = if ($env:BUILD_SOURCEVERSION) { $env:BUILD_SOURCEVERSION.Substring(0, [Math]::Min(8, $env:BUILD_SOURCEVERSION.Length)) } else { 'N/A' }
Write-Host "  Commit: $shortCommit" -ForegroundColor Gray
Write-Host ""

Write-Step "Validando prerequisites..."

Import-Module WebAdministration -ErrorAction Stop

$site = Get-Website -Name $siteName -ErrorAction SilentlyContinue
if (-not $site) {
    # Eurovip: primer deploy → se crea todo (carpetas, grupo de aplicaciones, sitio, permisos, firewall).
    Write-Warn "Sitio IIS '$siteName' no existe: se crea (primer deploy)."
    foreach ($path in @($bluePath, $greenPath)) { New-Item $path -ItemType Directory -Force | Out-Null }
    if (-not (Test-Path "IIS:\AppPools\$siteName")) { New-WebAppPool -Name $siteName | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$siteName" -Name managedRuntimeVersion -Value ''        # .NET Core: sin código administrado
    Set-ItemProperty "IIS:\AppPools\$siteName" -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    # Si el puerto pedido ya lo usa otro sitio (sin nombre de host), se toma el siguiente libre.
    $usados = @(Get-WebBinding | ForEach-Object { $b = $_.bindingInformation.Split(':'); if ($b[2] -eq $NombreHost) { $b[1] } })
    $pedido = $Puerto
    while ($usados -contains "$Puerto") { $Puerto++ }
    if ($Puerto -ne $pedido) { Write-Warn "El puerto $pedido ya lo usa otro sitio: Eurovip queda en el puerto $Puerto." }
    New-Website -Name $siteName -PhysicalPath $bluePath -ApplicationPool $siteName -Port $Puerto -HostHeader $NombreHost | Out-Null
    icacls $projectRoot /grant "IIS AppPool\${siteName}:(OI)(CI)M" /T /Q | Out-Null
    if ($Puerto -notin 80, 443 -and -not (Get-NetFirewallRule -DisplayName "Eurovip $Puerto" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName "Eurovip $Puerto" -Direction Inbound -Protocol TCP -LocalPort $Puerto -Action Allow | Out-Null
    }
    $site = Get-Website -Name $siteName
    Write-OK "Sitio '$siteName' creado en el puerto $Puerto $(if ($NombreHost) { "(host $NombreHost)" }) apuntando a Blue"
}
Write-OK "Sitio IIS encontrado: $siteName"
# .NET 8 Hosting Bundle (ASP.NET Core Module V2) instalado
if (-not ((& dotnet --list-runtimes 2>$null) -match 'Microsoft\.AspNetCore\.App 8\.')) {
    Write-Fail "Falta ASP.NET Core 8 (Hosting Bundle) en el servidor."
    exit 1
}

if (-not (Test-Path $ArtifactsPath)) {
    Write-Fail "Artifacts no encontrados en: $ArtifactsPath"
    exit 1
}
Write-OK "Artifacts encontrados"

# Crear carpetas Blue/Green si es la primera vez
foreach ($path in @($bluePath, $greenPath)) {
    if (-not (Test-Path $path)) {
        New-Item $path -ItemType Directory -Force | Out-Null
        Write-Warn "Carpeta creada (primer deploy): $path"
    }
}

# ============================================================
# 2. DETECTAR SLOT ACTIVO DESDE IIS (sin marker files)
#
# IIS guarda el physicalPath del sitio. Si termina en \Blue,
# Blue está activo → desplegamos en Green, y viceversa.
# ============================================================
Write-Step "Detectando slot activo desde IIS..."

$currentPath  = $site.physicalPath.TrimEnd('\')
$isBlueActive = $currentPath -like "*\Blue"
$activeSlot   = if ($isBlueActive) { "Blue"  } else { "Green" }
$targetSlot   = if ($isBlueActive) { "Green" } else { "Blue"  }
$targetPath   = if ($isBlueActive) { $greenPath } else { $bluePath }

Write-OK "Slot activo:  $activeSlot  →  $currentPath"
Write-OK "Slot destino: $targetSlot  →  $targetPath"

# ============================================================
# 3. PRESERVAR CONFIGURACIÓN DEL SERVIDOR (en memoria, sin backup en disco)
#
# Eurovip: los secretos viven SOLO en appsettings.Local.json; appsettings.json viene del repo (sin
# secretos) y debe actualizarse con cada versión, así que solo se conserva el .Local.
# Se guarda en memoria antes de limpiar el slot y se restaura después.
# ============================================================
Write-Step "Preservando configuración del servidor..."

$configInMemory = @{}
Get-ChildItem $targetPath -Filter "appsettings.Local.json" -ErrorAction SilentlyContinue | ForEach-Object {
    $configInMemory[$_.Name] = Get-Content $_.FullName -Raw -Encoding UTF8
    Write-OK "Preservado en memoria: $($_.Name)"
}

if ($configInMemory.Count -eq 0) {
    Write-Warn "No hay appsettings en el slot destino (primer deploy o slot vacío)"
}

# ============================================================
# 4. COPIAR ARTIFACTS AL SLOT DESTINO
#
# El slot destino está INACTIVO → podemos modificarlo sin
# interrumpir el servicio. El app pool sigue corriendo en el slot activo.
# ============================================================
Write-Step "Copiando artifacts al slot $targetSlot..."

# Limpiar slot destino
Get-ChildItem $targetPath -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Copiar todo desde el artifact
Copy-Item "$ArtifactsPath\*" $targetPath -Recurse -Force -ErrorAction Stop

$copiedFiles = (Get-ChildItem $targetPath -Recurse -File).Count
Write-OK "$copiedFiles archivos copiados al slot $targetSlot"

# Restaurar configuración del servidor (sobreescribe los appsettings del artifact)
if ($configInMemory.Count -gt 0) {
    foreach ($fileName in $configInMemory.Keys) {
        $configInMemory[$fileName] | Set-Content "$targetPath\$fileName" -Encoding UTF8 -NoNewline
        Write-OK "Restaurado: $fileName"
    }
}
# Eurovip: la configuración real del pipeline (variable secreta) manda sobre la guardada.
if ($appSettingsLocalB64) {
    [IO.File]::WriteAllBytes("$targetPath\appsettings.Local.json", [Convert]::FromBase64String($appSettingsLocalB64))
    Write-OK "appsettings.Local.json escrito desde la variable secreta del pipeline"
} elseif (-not (Test-Path "$targetPath\appsettings.Local.json")) {
    Write-Warn "ACCION REQUERIDA: falta appsettings.Local.json (cadena de conexión real) en $targetPath."
    Write-Warn "Cargar la variable secreta EurovipAppSettingsLocal en el pipeline o copiar el archivo a mano."
    Write-Warn "Sin ese archivo el /health falla (no llega a la base) y el deploy vuelve atrás."
}

# ============================================================
# 4.5. PRESERVAR UPLOADS DEL SLOT ACTIVO
#
# [2026-08-08] Fix: las imágenes/HTMLs subidos por usuarios (carpetas privadas,
# flyers cliente, logos de agencias, etc.) viven en wwwroot\imagenes\ del slot
# ACTIVO. El artifact solo tiene los archivos que estaban en git al momento
# del build — NO los uploads posteriores. Sin este paso, cada deploy borraba
# TODAS las imágenes que los usuarios subieron entre deploys.
#
# Estrategia: después de copiar el artifact al slot destino, sincronizamos
# las carpetas de uploads del slot ACTIVO → slot destino usando robocopy
# /MIR (mirror). Los archivos del artifact que estén en las mismas rutas se
# sobreescriben con la versión del slot activo (siempre más reciente).
# ============================================================
Write-Step "Preservando uploads del slot activo..."

$activePath = if ($isBlueActive) { $bluePath } else { $greenPath }
$foldersToPreserve = @(
    "wwwroot\imagenes",   # fotos subidas desde la pantalla (hoteles, circuitos, destinos, alojamientos, excursiones)
    "wwwroot\uploads",    # flyers generados, fondos de flyers y fotos de perfil
    "Logs"                # registros de la aplicación (Serilog)
)

foreach ($folder in $foldersToPreserve) {
    $src = Join-Path $activePath $folder
    $dst = Join-Path $targetPath $folder
    if (-not (Test-Path $src)) {
        Write-Warn "$folder no existe en slot activo (primer deploy o carpeta vacia)"
        continue
    }
    # Crear destino si no existe
    if (-not (Test-Path $dst)) {
        New-Item -Path $dst -ItemType Directory -Force | Out-Null
    }
    # robocopy /E copia recursivo sin borrar destino; /XO evita reescribir
    # archivos idénticos; /NFL /NDL /NJH /NJS calla el output. Códigos de
    # salida <8 son OK (1-7 significan que se copiaron archivos).
    $robocopyOut = & robocopy $src $dst /E /XO /NFL /NDL /NJH /NJS /NP 2>&1
    $rc = $LASTEXITCODE
    $count = (Get-ChildItem $src -Recurse -File -ErrorAction SilentlyContinue).Count
    if ($rc -lt 8) {
        Write-OK "$folder → $count archivos preservados"
    } else {
        Write-Warn "$folder → robocopy exit $rc (revisar manualmente)"
    }
}

# ============================================================
# 5. SWITCH: APUNTAR IIS AL SLOT DESTINO
#
# Solo en este paso hay downtime (2-3 segundos mientras el app pool
# se detiene y reinicia con la nueva ruta).
# ============================================================
Write-Step "Ejecutando switch a $targetSlot..."

$appPool = $site.applicationPool

try {
    # Detener app pool con espera de estado estable y reintentos
    Stop-AppPoolSafe -Name $appPool

    # Cambiar physicalPath → IIS ahora apunta al slot destino
    Set-ItemProperty "IIS:\Sites\$siteName" -Name physicalPath -Value $targetPath -ErrorAction Stop

    # Iniciar app pool con el nuevo código
    $appcmd = Join-Path $env:SystemRoot "System32\inetsrv\appcmd.exe"
    & $appcmd start apppool /apppool.name:"$appPool" 2>&1 | Out-Null
    Start-Sleep -Seconds 15

    Write-OK "Switch completado. IIS apunta a: $targetSlot"

} catch {
    Write-Fail "Error durante el switch: $($_.Exception.Message)"
    Write-Warn "Revertiendo a $activeSlot..."

    try {
        Set-ItemProperty "IIS:\Sites\$siteName" -Name physicalPath -Value $currentPath -ErrorAction SilentlyContinue
        Start-WebAppPool -Name $appPool -ErrorAction SilentlyContinue
        Write-OK "IIS revertido a: $activeSlot"
    } catch {
        Write-Fail "ERROR CRÍTICO en reversión: $($_.Exception.Message)"
    }

    exit 1
}

# ============================================================
# 6. HEALTH CHECK HTTP
#
# Detecta el puerto automáticamente desde el binding HTTP del sitio.
# Acepta HTTP 200-299 como respuesta saludable.
# Requiere que BackTITAN tenga el endpoint /health configurado
# como público (app.MapHealthChecks("/health").AllowAnonymous()).
#
# NOTA: Si el sitio solo tiene binding HTTPS y PowerShell < 6,
# el health check fallará por certificado. En ese caso, agregar
# un binding HTTP en localhost:8080 (interno, no expuesto al exterior).
# ============================================================
Write-Step "Ejecutando health check ($HealthCheckPath)..."

# Auto-detectar URL desde binding IIS
$httpBinding = Get-WebBinding -Name $siteName |
               Where-Object { $_.protocol -eq "http" } |
               Select-Object -First 1

if ($httpBinding) {
    # bindingInformation = "IP:Puerto:Hostname"
    $bindingParts = $httpBinding.bindingInformation.Split(':')
    $port         = $bindingParts[1]
    $hostname     = $bindingParts[2]

    # Si el binding tiene hostname específico, usarlo. Si no, usar localhost.
    $siteHost  = if ($hostname) { $hostname } else { "localhost" }
    $healthUrl = "http://$siteHost$(if ($port -ne '80') { ":$port" })$HealthCheckPath"
} else {
    # Fallback a HTTPS (requiere PowerShell 7+ para -SkipCertificateCheck)
    $httpsBinding = Get-WebBinding -Name $siteName |
                    Where-Object { $_.protocol -eq "https" } |
                    Select-Object -First 1
    $bindingParts = if ($httpsBinding) { $httpsBinding.bindingInformation.Split(':') } else { @("", "443", "") }
    $port         = $bindingParts[1]
    $hostname     = $bindingParts[2]
    $siteHost     = if ($hostname) { $hostname } else { "localhost" }
    $healthUrl    = "https://$siteHost$(if ($port -ne '443') { ":$port" })$HealthCheckPath"
    Write-Warn "Solo se encontró binding HTTPS. Usando: $healthUrl"
}

Write-OK "Health check URL: $healthUrl"

$healthy = $false
for ($i = 1; $i -le $HealthCheckRetries; $i++) {
    Write-Host "    Intento $i/$HealthCheckRetries..." -ForegroundColor Gray
    try {
        $invokeParams = @{
            Uri              = $healthUrl
            UseBasicParsing  = $true
            TimeoutSec       = 15
            ErrorAction      = "Stop"
        }
        # PowerShell 7+: deshabilitar validación de cert para HTTPS en localhost
        if ($healthUrl.StartsWith("https") -and $PSVersionTable.PSVersion.Major -ge 6) {
            $invokeParams["SkipCertificateCheck"] = $true
        }

        $response = Invoke-WebRequest @invokeParams

        if ($response.StatusCode -ge 200 -and $response.StatusCode -le 299) {
            Write-OK "Health check OK (HTTP $($response.StatusCode))"
            $healthy = $true
            break
        }

        Write-Warn "Respuesta inesperada: HTTP $($response.StatusCode)"

    } catch {
        Write-Warn "Intento $i falló: $($_.Exception.Message)"
    }

    if ($i -lt $HealthCheckRetries) {
        Start-Sleep -Seconds $HealthCheckWaitSeconds
    }
}

# ============================================================
# 7. ROLLBACK AUTOMÁTICO SI HEALTH CHECK FALLA
# ============================================================
if (-not $healthy) {
    Write-Fail "Health check falló tras $HealthCheckRetries intentos."
    Write-Warn "Ejecutando rollback automático a $activeSlot..."

    try {
        Stop-AppPoolSafe -Name $appPool
        Set-ItemProperty "IIS:\Sites\$siteName" -Name physicalPath -Value $currentPath -ErrorAction Stop
        $appcmd = Join-Path $env:SystemRoot "System32\inetsrv\appcmd.exe"
        & $appcmd start apppool /apppool.name:"$appPool" 2>&1 | Out-Null
        Start-Sleep -Seconds 3

        Write-OK "Rollback completado. Slot activo restaurado: $activeSlot"
        Write-Warn "Revisar logs de la aplicación en: $targetPath\logs\"
    } catch {
        Write-Fail "ERROR CRÍTICO: Rollback falló. Intervención manual requerida."
        Write-Fail "Verificar IIS manualmente y apuntar el sitio a: $currentPath"
    }

    exit 1
}

# ============================================================
# 8. DEPLOY EXITOSO
# ============================================================
Write-Banner "DEPLOY EXITOSO"
Write-Host "  Slot activo: $targetSlot" -ForegroundColor Green
Write-Host "  Ruta:        $targetPath" -ForegroundColor Green
Write-Host "  Build:       $env:BUILD_BUILDNUMBER" -ForegroundColor Green
Write-Host "  Commit:      $env:BUILD_SOURCEVERSION" -ForegroundColor Green
Write-Host ""

exit 0
