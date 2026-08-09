using AxiomOps.Services;
using Microsoft.Extensions.DependencyInjection;

// Smoke test for the AxiomOps.Services library.
// Usage:
//   set AXIOM_BASE_URL=https://axiomcore-app1-gtpXXX.installprogram.eu
//   set AXIOM_TOKEN=<okta bearer token>
//   dotnet run --project samples/AxiomOps.Console

var baseUrl = Environment.GetEnvironmentVariable("AXIOM_BASE_URL");
var token = Environment.GetEnvironmentVariable("AXIOM_TOKEN");

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Set AXIOM_BASE_URL and AXIOM_TOKEN environment variables to run this sample.");
    return;
}

var services = new ServiceCollection()
    .AddAxiomOpsServices(options =>
    {
        options.BaseUrl = baseUrl;
        options.AccessToken = token;
    })
    .BuildServiceProvider();

var health = services.GetRequiredService<IHealthService>();
var state = await health.GetApplianceStateAsync();

Console.WriteLine($"Success:     {state.Success}");
Console.WriteLine($"Environment: {state.DataObject?.ApplianceMetaData?.EnvironmentName}");
Console.WriteLine($"Healthy:     {state.DataObject?.ApplianceHealth?.IsHealthy}");
Console.WriteLine($"Games:       {state.DataObject?.ApplianceMetaData?.InstalledGames?.Count}");

var games = services.GetRequiredService<IGamesService>();
var installed = await games.GetInstalledDatabaseGameRecordsAsync();

foreach (var game in installed.DataObject ?? [])
{
    Console.WriteLine($"  [{game.ModuleId}/{game.ClientId}] {game.DisplayName}");
}
