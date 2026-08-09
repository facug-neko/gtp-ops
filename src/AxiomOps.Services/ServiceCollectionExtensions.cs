using AxiomOps.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AxiomOps.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers every Axiom Administrator Core service (one per Postman folder)
    /// plus the shared <see cref="AxiomEnvironmentContext"/>. Select the target
    /// environment at runtime via the context, or configure static defaults here.
    /// </summary>
    public static IServiceCollection AddAxiomOpsServices(this IServiceCollection services, Action<AxiomOpsOptions>? configure = null)
    {
        services.Configure(configure ?? (_ => { }));
        services.TryAddSingleton<AxiomEnvironmentContext>();
        services.AddTransient<AxiomEnvironmentHandler>();

        AddService<IAuthorizationService, AuthorizationService>(services);
        AddService<IBetSettingsService, BetSettingsService>(services);
        AddService<ICasinoSettingsService, CasinoSettingsService>(services);
        AddService<IEnvironmentsService, EnvironmentsService>(services);
        AddService<IFreeGamesService, FreeGamesService>(services);
        AddService<IGamesService, GamesService>(services);
        AddService<IGameSettingsService, GameSettingsService>(services);
        AddService<IHealthService, HealthService>(services);
        AddService<ILaunchService, LaunchService>(services);
        AddService<IManageService, ManageService>(services);
        AddService<IMobileSettingsService, MobileSettingsService>(services);
        AddService<IProgressivesService, ProgressivesService>(services);
        AddService<IUploadService, UploadService>(services);
        AddService<IUserAccountsService, UserAccountsService>(services);

        return services;
    }

    private static void AddService<TInterface, TImplementation>(IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>((provider, http) =>
        {
            // Placeholder host — AxiomEnvironmentHandler rewrites it per request
            // to the currently selected environment.
            http.BaseAddress = AxiomEnvironmentHandler.PlaceholderBaseAddress;
            http.Timeout = provider.GetRequiredService<IOptions<AxiomOpsOptions>>().Value.Timeout;
        })
        .AddHttpMessageHandler<AxiomEnvironmentHandler>();
    }
}
