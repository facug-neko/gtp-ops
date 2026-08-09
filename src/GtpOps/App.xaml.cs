using System.Windows;
using AxiomOps.Compass.Gtp;
using CommunityToolkit.Mvvm.Messaging;
using GtpOps.Services;
using GtpOps.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GtpOps;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A UI/binding glitch shouldn't kill the app — surface it and keep running.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"{args.Exception.GetType().Name}: {args.Exception.Message}" +
                (args.Exception.InnerException is { } inner ? $"\n\n{inner.GetType().Name}: {inner.Message}" : string.Empty),
                "Error inesperado",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton<IGtpPortalService, GtpPortalService>();
        builder.Services.AddSingleton<IGameCatalog, GameCatalog>();
        builder.Services.AddSingleton<IDeliverableOverrideStore, DeliverableOverrideStore>();
        builder.Services.AddSingleton<IBackendDeliverableStore, BackendDeliverableStore>();
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<DeliverablesViewModel>();
        builder.Services.AddTransient<PreCertViewModel>();
        builder.Services.AddTransient<HelpfilesViewModel>();
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
