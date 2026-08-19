using System.Windows;
using AxiomOps.Compass;
using AxiomOps.Services;
using AxiomOps.UI.Services;
using AxiomOps.UI.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AxiomOps.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddAxiomOpsServices();
        builder.Services.AddSingleton<ICompassCliService, CompassCliService>();
        builder.Services.AddSingleton<IAxiomKeyStore, AxiomKeyStore>();
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        builder.Services.AddSingleton<EnvironmentSelectorViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<BulkBetSettingsViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddSingleton<GameInstallDiagnosticsService>();
        builder.Services.AddTransient<GamesViewModel>();
        builder.Services.AddTransient<LaunchViewModel>();
        builder.Services.AddTransient<FilesViewModel>();
        builder.Services.AddTransient<LogsViewModel>();
        builder.Services.AddTransient<TestDataViewModel>();
        builder.Services.AddTransient<GameEventDataViewModel>();
        builder.Services.AddTransient<CreateUserViewModel>();
        builder.Services.AddTransient<DeployGameViewModel>();
        builder.Services.AddSingleton<TestDataCatalogService>();
        builder.Services.AddTransient<PlayRepositoryViewModel>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _host.Start();

        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
