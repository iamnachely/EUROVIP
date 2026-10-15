# Despliegue de EUROVIP (BackOffice) en el servidor

Igual que BackTiTan: **Azure DevOps** compila y un **agente instalado en el servidor** publica en IIS con
**Blue-Green** (`DeploymentScripts/Deploy.ps1`): copia la versión nueva al slot que no está en uso, cambia
IIS a ese slot, comprueba `/health` (la app arranca **y** llega a la base de datos) y, si falla, vuelve
solo a la versión anterior. Las fotos y archivos subidos (`wwwroot\imagenes`, `wwwroot\uploads`) y los
`Logs` se conservan en cada despliegue.

> El despliegue es **manual**: subir cambios a `main` NO publica nada. Para publicar:
> **Azure DevOps → Pipelines → EurovipBack → Run pipeline** (y aprobar en el Environment).

---

## Lo que hay que hacer UNA sola vez en el servidor (como administrador)

### 1. .NET 8 Hosting Bundle
Si el servidor no tiene ASP.NET Core 8, instalar el **ASP.NET Core 8.0 Hosting Bundle** y reiniciar IIS (`iisreset`).

### 2. Carpetas, grupo de aplicaciones y sitio en IIS
```powershell
Import-Module WebAdministration
New-Item C:\inetpub\wwwroot\eurovip\Blue  -ItemType Directory -Force
New-Item C:\inetpub\wwwroot\eurovip\Green -ItemType Directory -Force

New-WebAppPool -Name "eurovip"
Set-ItemProperty IIS:\AppPools\eurovip -Name managedRuntimeVersion -Value ""     # Sin código administrado (.NET Core)
Set-ItemProperty IIS:\AppPools\eurovip -Name startMode -Value AlwaysRunning

# Cambiar el puerto / nombre de host según cómo se vaya a entrar (ej. eurovip.midominio.com)
New-Website -Name "eurovip" -PhysicalPath C:\inetpub\wwwroot\eurovip\Blue -ApplicationPool "eurovip" -Port 8090
```
Después, en el Administrador de IIS, agregar el enlace **https** con su certificado si se va a entrar desde fuera.

### 3. Configuración real (`appsettings.Local.json`)
El repositorio no lleva contraseñas. Copiar el `appsettings.Local.json` (cadena de conexión a
TropitoursVDT, WhatsApp, etc.) en **las dos** carpetas:
```
C:\inetpub\wwwroot\eurovip\Blue\appsettings.Local.json
C:\inetpub\wwwroot\eurovip\Green\appsettings.Local.json
```
Si la base está en el mismo servidor, la cadena de conexión puede usar `localhost`.
El despliegue conserva estos archivos (no los pisa).

### 4. Permisos de escritura
La identidad del grupo `IIS AppPool\eurovip` necesita **modificar** en `C:\inetpub\wwwroot\eurovip`
(para guardar fotos, flyers y logs):
```powershell
icacls C:\inetpub\wwwroot\eurovip /grant "IIS AppPool\eurovip:(OI)(CI)M"
```

### 5. Agente de Azure DevOps en el servidor
1. Azure DevOps → **Organization settings → Agent pools → Add pool** → nombre **`Eurovip-Agents`** (self-hosted).
2. Dentro del pool → **New agent → Windows** → descargar y en el servidor ejecutar `config.cmd`
   (URL `https://dev.azure.com/grupovdt`, un PAT con permiso *Agent Pools (read, manage)*, pool `Eurovip-Agents`),
   **como servicio** y con una cuenta con permisos de administrador de IIS.

### 6. Environment y pipelines en el proyecto EurovipBack
1. **Pipelines → Environments → New environment** → `Eurovip-Produccion` → *Approvals and checks* → aprobación manual.
2. **Pipelines → New pipeline → Azure Repos Git → EurovipBack → Existing YAML file** → `/azure-pipelines.yml`.
3. Repetir con `/azure-pipelines-rollback.yml` (para volver a la versión anterior si hiciera falta).

---

## Configuración real sin copiar archivos (variable secreta)
En el pipeline → **Edit → Variables → New variable** → nombre `EurovipAppSettingsLocal`, valor = el
contenido de `appsettings.Local.json` en **base64**, y marcar **Keep this value secret**.
En PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("appsettings.Local.json"))`.
Con esa variable, `Deploy.ps1` escribe el archivo en cada despliegue y **crea solo el sitio IIS
`eurovip`** (carpetas, grupo de aplicaciones, permisos y firewall) la primera vez, con el puerto de
la variable `eurovipPuerto` (8090) — los pasos 2, 3 y 4 de arriba pasan a ser opcionales.

## Publicar una versión
**Pipelines → (pipeline de Eurovip) → Run pipeline** → aprobar → al terminar debe decir `DEPLOY EXITOSO`.

## Volver a la versión anterior
**Pipelines → (pipeline de rollback) → Run pipeline** → escribir `ROLLBACK` → Run.

## Comprobar que está bien
`http://localhost:8090/health` en el servidor (o el puerto que se haya puesto) debe responder **Healthy**.
