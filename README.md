# AxiomOps

Cliente .NET para la API **Axiom Administrator Core** (entornos Axiom de Games Global / installprogram.eu).

## Estructura

| Proyecto | Descripción |
|---|---|
| `src/AxiomOps.Services` | Biblioteca de servicios: un servicio tipado por cada carpeta de la colección Postman `AxiomAdministratorCore`. |
| `src/AxiomOps.Compass` | Wrapper del CLI `compass` (listado de ambientes vía Okta) + key-store de api-keys compartido con axiom-compass. |
| `src/AxiomOps.UI` | App WPF (MVVM con CommunityToolkit.Mvvm + Hosting): selector de ambientes → dashboard de salud. |
| `samples/AxiomOps.Console` | Consola de ejemplo que muestra el registro por DI y un par de llamadas. |

## UI

Flujo actual:

1. **Selector de ambientes** — lista `compass portal get-environments` (requiere `compass login` previo en una terminal), filtro por texto y "solo Axiom". Al seleccionar un ambiente precarga su api-key desde `~/.axiom-compass/keys.json` (mismo store que axiom-compass).
2. **Conectar** — setea el `AxiomEnvironmentContext` (base URL = `healthHostname` del ambiente) y valida la key contra `GET /GameSettings/GameProviders`. Un 401/403 invalida la key guardada (ambiente regenerado) y pide una nueva.
3. **Dashboard** — `GET /Health`: salud del appliance, fallas (servicios/sitios/app pools), stats por host (CPU/RAM), juegos instalados y última provisión.

```powershell
dotnet run --project src/AxiomOps.UI
```

## Servicios (1:1 con las carpetas de la colección)

| Carpeta Postman | Interfaz | Cobertura |
|---|---|---|
| Authorization | `IAuthorizationService` | API keys (crear/listar/revocar), bypass global de seguridad |
| BetSettings | `IBetSettingsService` | Settings de apuesta por usuario/juego, plantillas de multiplicador, validación |
| CasinoSettings | `ICasinoSettingsService` | Catálogos: monedas, países, idiomas, casinos instalados, mercados regulados… |
| Environments | `IEnvironmentsService` | Última provisión, inventario de software por host |
| FreeGames | `IFreeGamesService` | Ofertas de free games y opciones disponibles |
| Games | `IGamesService` | Juegos instalados (DB y full record), chequeo de dependencias |
| GameSettings | `IGameSettingsService` | Providers, sesiones, filter maps, presets 32/64 bits, force game settings… |
| Health | `IHealthService` | Estado del appliance, salud por host, entradas de hosts |
| Launch | `ILaunchService` | URL de lanzamiento de juego, links de lobby y playcheck |
| Manage | `IManageService` | Contenido (archivos/carpetas/CDN), IIS (sites y app pools), servicios Windows |
| MobileSettings | `IMobileSettingsService` | Lobbies, framework por defecto, versiones de Titan |
| Progressives | `IProgressivesService` | Settings, jackpots, bet log, wins, validación de premios |
| Upload | `IUploadService` | Subida de contenido, presets, services, test data (form-data o por URL) |
| UserAccounts | `IUserAccountsService` | Cuentas de usuario, balance, moneda, sesiones, LVCS |

Todas las respuestas vienen envueltas en `AxiomResponse<T>`:

```csharp
public class AxiomResponse<T>
{
    public bool Success { get; set; }
    public string? CustomMessage { get; set; }
    public T? DataObject { get; set; }        // payload tipado
    public JsonElement? ResultSets { get; set; }
}
```

Los errores HTTP (4xx/5xx) y los fallos de deserialización lanzan `AxiomApiException`
(con `StatusCode`, `RequestUri` y `ResponseBody` para diagnóstico).

## Uso

```csharp
services.AddAxiomOpsServices();

// Ambiente conmutable en runtime (lo que usa la UI):
var context = provider.GetRequiredService<AxiomEnvironmentContext>();
context.SetEnvironment("gtp714", apiKey: "<x-api-key del ambiente>");

// O configuración estática (scripts / consola):
services.AddAxiomOpsServices(options =>
{
    options.BaseUrl = "https://axiomcore-app1-gtp714.installprogram.eu";
    options.ApiKey = "<x-api-key>";                 // Axiom Admin auth
    // options.AccessToken = "<bearer de Okta>";    // alternativa Bearer
    // options.AccessTokenProvider = async ct => await miOktaClient.GetTokenAsync(ct);
});
```

```csharp
public class MyViewModel(IHealthService health, IGamesService games)
{
    public async Task LoadAsync()
    {
        var state = await health.GetApplianceStateAsync();
        var installed = await games.GetInstalledGameRecordsAsync();
        // state.DataObject, installed.DataObject ...
    }
}
```

### Autenticación

Dos mecanismos soportados:

- **`x-api-key` por ambiente** (recomendado — es lo que Axiom Admin acepta de forma confiable; el JWT de Okta que emite Compass 1.12 es rechazado con 401). Las keys se persisten en `~/.axiom-compass/keys.json`, compartido con la herramienta axiom-compass.
- **Bearer token de Okta** (`derivco.okta-emea.com`, expira ~2 h) — como usa la colección de Postman. Para sesiones largas configurá `AccessTokenProvider` con refresh.

### Ejemplo rápido

```powershell
$env:AXIOM_BASE_URL = "https://axiomcore-app1-gtp714.installprogram.eu"
$env:AXIOM_TOKEN = "<token>"
dotnet run --project samples/AxiomOps.Console
```

## Build

```powershell
dotnet build AxiomOps.sln
```

Target: `net10.0`. Dependencia única de la biblioteca: `Microsoft.Extensions.Http`.
