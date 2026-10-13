# EUROVIP — BackOffice

BackOffice de **Eurovip**, la nueva web de Dominicana Tours para vender viajes a República Dominicana.
Carga manual de ofertas de hotel, circuitos, hoteles de la Home, cargas y reportes. Todo en español y
**con vuelo opcional**.

## Tecnología
- ASP.NET Core 8 MVC + Razor, Dapper, SQL Server
- Identity con store propio sobre Dapper (cookies)
- Serilog, DocumentFormat.OpenXml (lectura/generación de Excel)

## Estructura
```
BackEurovip.sln
├── Common/            Entidades y mensajes compartidos
├── EuroVip/           Web MVC (Controllers, Repositories, Views, wwwroot)
├── Database/          EUROVIP_BaseDeDatos.sql — ÚNICO archivo con todos los cambios de BD explicados
└── Docs/              Plan del proyecto
```

## Base de datos
Todas las tablas y procedimientos viven en el esquema **`Eurovip`** de `TropitoursVDT`.
Eurovip no lee ni escribe en `dbo`. Los catálogos se copiaron de ViajeVDT (solo lectura).

## Configuración local
La cadena de conexión real **no está en el repositorio**. Copia
`EuroVip/appsettings.Local.example.json` como `EuroVip/appsettings.Local.json` (ignorado por git)
y pon el servidor, el usuario y la contraseña.

```bash
dotnet run --project EuroVip/EuroVip.csproj
```
