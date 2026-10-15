# ============================================================
# Instalar-Eurovip.ps1 — PRIMERA instalación de EUROVIP (BackOffice) en el servidor IIS.
#
# Se ejecuta EN EL SERVIDOR, como administrador, desde la carpeta donde se copiaron:
#     eurovip.zip                (el paquete publicado)
#     appsettings.Local.json     (la configuración real: cadena de conexión, WhatsApp…)
#     Instalar-Eurovip.ps1       (este script)
#
#   PowerShell (como administrador):
#     cd C:\eurovip-instalar
#     powershell -ExecutionPolicy Bypass -File .\Instalar-Eurovip.ps1 -Puerto 8090
#     (o con nombre de host:  -Puerto 80 -NombreHost eurovip.midominio.com)
#
# Deja todo como lo espera el despliegue Blue-Green (Deploy.ps1 / azure-pipelines.yml):
#   C:\inetpub\wwwroot\eurovip\Blue   ← versión instalada (sitio IIS "eurovip" apunta aquí)
#   C:\inetpub\wwwroot\eurovip\Green  ← vacío, lo usará la próxima versión
# Se puede volver a ejecutar: no borra fotos (wwwroot\imagenes, wwwroot\uploads) ni Logs.
# ============================================================
#Requires -RunAsAdministrator
param(
    [string]$Zip = (Join-Path $PSScriptRoot 'eurovip.zip'),
    [int]$Puerto = 8090,
    [string]$NombreHost = ''
)
$ErrorActionPreference = 'Stop'
$sitio = 'eurovip'
$base  = 'C:\inetpub\wwwroot\eurovip'
$blue  = Join-Path $base 'Blue'
$green = Join-Path $base 'Green'

function Paso($t) { Write-Host "`n>>> $t" -ForegroundColor Yellow }
function Ok($t)   { Write-Host "    [OK] $t" -ForegroundColor Green }
function Ojo($t)  { Write-Host "    [AVISO] $t" -ForegroundColor DarkYellow }

Paso 'Comprobando requisitos'
if (-not (Test-Path $Zip)) { throw "No encuentro el paquete: $Zip" }
$runtimes = & dotnet --list-runtimes 2>$null
if (-not ($runtimes -match 'Microsoft\.AspNetCore\.App 8\.')) {
    Write-Host '    Falta ASP.NET Core 8 en el servidor. Instala el ".NET 8 Hosting Bundle":' -ForegroundColor Red
    Write-Host '    https://dotnet.microsoft.com/download/dotnet/8.0  → ASP.NET Core Runtime → Windows → Hosting Bundle' -ForegroundColor Red
    Write-Host '    Después ejecuta "iisreset" y vuelve a lanzar este script.' -ForegroundColor Red
    exit 1
}
Ok 'ASP.NET Core 8 instalado'
Import-Module WebAdministration
Ok 'IIS disponible'

# ¿El puerto/host ya lo usa OTRO sitio?
$enUso = Get-WebBinding | Where-Object {
    $p = $_.bindingInformation.Split(':'); $p[1] -eq "$Puerto" -and $p[2] -eq $NombreHost
} | Where-Object { $_.ItemXPath -notmatch "\[@name='$sitio'" }
if ($enUso) { throw "El puerto $Puerto (host '$NombreHost') ya lo usa otro sitio de IIS. Elige otro con -Puerto." }

Paso 'Carpetas'
New-Item $blue, $green -ItemType Directory -Force | Out-Null
Ok "$blue y $green"

Paso 'Copiando la aplicación a Blue'
$appPoolExiste = Test-Path "IIS:\AppPools\$sitio"
if ($appPoolExiste) { & "$env:SystemRoot\System32\inetsrv\appcmd.exe" stop apppool /apppool.name:$sitio 2>&1 | Out-Null; Start-Sleep 3 }
$tmp = Join-Path $env:TEMP "eurovip-$(Get-Date -Format yyyyMMddHHmmss)"
Expand-Archive -Path $Zip -DestinationPath $tmp -Force
# robocopy: copia/actualiza sin borrar lo que ya está (fotos, uploads, logs, appsettings del servidor)
robocopy $tmp $blue /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falló (código $LASTEXITCODE)" }
Remove-Item $tmp -Recurse -Force
Ok "$((Get-ChildItem $blue -Recurse -File).Count) archivos en Blue"

Paso 'Configuración real (appsettings.Local.json)'
$local = Join-Path $PSScriptRoot 'appsettings.Local.json'
if (Test-Path $local) {
    Copy-Item $local $blue -Force; Copy-Item $local $green -Force
    Ok 'Copiado a Blue y Green'
} elseif (Test-Path (Join-Path $blue 'appsettings.Local.json')) {
    Ok 'Ya estaba en Blue (no se toca)'
} else {
    Ojo 'No hay appsettings.Local.json junto al script: el sitio no llegará a la base hasta copiarlo en Blue y Green.'
}

Paso 'Grupo de aplicaciones y sitio IIS'
if (-not $appPoolExiste) {
    New-WebAppPool -Name $sitio | Out-Null
    Ok "Grupo de aplicaciones '$sitio' creado"
}
Set-ItemProperty "IIS:\AppPools\$sitio" -Name managedRuntimeVersion -Value ''      # .NET Core: sin código administrado
Set-ItemProperty "IIS:\AppPools\$sitio" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$sitio" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
if (-not (Get-Website -Name $sitio)) {
    New-Website -Name $sitio -PhysicalPath $blue -ApplicationPool $sitio -Port $Puerto -HostHeader $NombreHost | Out-Null
    Ok "Sitio '$sitio' creado en el puerto $Puerto $(if ($NombreHost) { "(host $NombreHost)" })"
} else {
    Set-ItemProperty "IIS:\Sites\$sitio" -Name physicalPath -Value $blue
    Ok "Sitio '$sitio' ya existía: ahora apunta a Blue"
}

Paso 'Permisos de escritura (fotos, flyers, logs)'
icacls $base /grant "IIS AppPool\${sitio}:(OI)(CI)M" /T /Q | Out-Null
Ok "IIS AppPool\$sitio puede modificar $base"

if ($Puerto -notin 80, 443) {
    Paso "Firewall (puerto $Puerto)"
    if (-not (Get-NetFirewallRule -DisplayName "Eurovip $Puerto" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName "Eurovip $Puerto" -Direction Inbound -Protocol TCP -LocalPort $Puerto -Action Allow | Out-Null
        Ok "Regla de entrada creada para el puerto $Puerto"
    } else { Ok 'La regla ya existía' }
}

Paso 'Arrancando y comprobando /health'
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" start apppool /apppool.name:$sitio 2>&1 | Out-Null
Start-Website -Name $sitio -ErrorAction SilentlyContinue
$hostUrl = if ($NombreHost) { $NombreHost } else { 'localhost' }
$url = "http://${hostUrl}:$Puerto/health"
$bien = $false
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep 5
    try {
        $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 20
        Write-Host "    Intento ${i}: HTTP $($r.StatusCode) $($r.Content)" -ForegroundColor Gray
        if ($r.StatusCode -eq 200) { $bien = $true; break }
    } catch { Write-Host "    Intento ${i}: $($_.Exception.Message)" -ForegroundColor Gray }
}
Write-Host ''
if ($bien) {
    Write-Host '============================================================' -ForegroundColor Cyan
    Write-Host "  EUROVIP INSTALADO: http://${hostUrl}:$Puerto" -ForegroundColor Green
    Write-Host '============================================================' -ForegroundColor Cyan
} else {
    Write-Host "  El sitio no respondió bien en $url." -ForegroundColor Red
    Write-Host "  Revisa: $blue\Logs (registro de la app) y que appsettings.Local.json esté en $blue." -ForegroundColor Red
    exit 1
}
