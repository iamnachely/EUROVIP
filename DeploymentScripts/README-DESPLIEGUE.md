# Despliegue de EUROVIP (BackOffice) en el servidor

Igual que BackTiTan: **Azure DevOps** compila y un **agente instalado en el servidor** publica en IIS con
**Blue-Green** (`DeploymentScripts/Deploy.ps1`): copia la versión nueva al slot que no está en uso, cambia
IIS a ese slot, comprueba `/health` (la app arranca **y** llega a la base de datos) y, si falla, vuelve
solo a la versión anterior. Las fotos y archivos subidos (`wwwroot\imagenes`, `wwwroot\multimedia`,
`wwwroot\uploads`) y los `Logs` se conservan en cada despliegue.

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

### 3b. Fotos (carpeta de fotos de Eurovip)
Todas las fotos se guardan en la carpeta del sitio de fotos y se ven desde `fotos.eurovip.com`:

| Qué | Carpeta (en el servidor) | Se ve en |
|---|---|---|
| Base | `C:\inetpub\wwwroot\photosVacacionalparce\eurovip\` | `https://fotos.eurovip.com/eurovip/` |
| Destinos, países, hoteles, alojamientos, excursiones, traslados | `…\eurovip\destinos\{destino}\`, `paises\{código}\`, `hoteles\{n.º}\`, `alojamientos\{id}\`, `excursiones\{id}\`, `traslados\{id}\` | `…/eurovip/destinos/punta-cana/{código}.webp` |
| Circuitos (portada, intermedia, galería) | `…\eurovip\circuitos\{circuito}\` | |
| Itinerario de circuito | `…\eurovip\circuitos\itinerario\{circuito}\` | |
| Slider (web, tablet, móvil) | `…\eurovip\slider\`, `slider\md\`, `slider\sm\` | `…/eurovip/slider/…` |

- Los nombres de archivo son un código aleatorio (`3f2a9c1e….webp`): nunca se pisa una foto. El slider conserva el nombre que se le da al cargarlo.
- Se configura en `appsettings.json`: `Multimedia:CarpetaFisica`, `Multimedia:Url` y `Eurovip:Slider` (con `Anterior` = la carpeta de antes del slider).
- **Permisos:** la identidad `IIS AppPool\eurovipBack` necesita **Modificar** en `C:\inetpub\wwwroot\photosVacacionalparce\eurovip`.
- **Primera vez:** en la app del servidor, **Herramientas → Fotos → Pasar las fotos de antes** (Superadministrador): copia a la
  carpeta nueva las fotos que estaban en `wwwroot\imagenes` y los archivos del slider de la carpeta anterior. No borra nada.
- La base es la misma para la app del servidor y la de cada computadora, pero la carpeta `C:\inetpub\…` está en el servidor:
  las fotos se suben desde la app del servidor (desde otra computadora se guardarían en su propio disco).

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
