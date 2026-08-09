using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace GtpOps.ViewModels;

/// <summary>
/// Shell. Opens on the projects browser and swaps to the deliverables validator
/// when a project is chosen. The CurrentViewModel indirection keeps room for
/// more GTP screens (helpfiles, etc.).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public MainWindowViewModel(IServiceProvider services, IMessenger messenger)
    {
        _services = services;

        var projects = services.GetRequiredService<ProjectsViewModel>();
        _currentViewModel = projects;
        _ = projects.LoadCatalogCommand.ExecuteAsync(null);

        messenger.Register<OpenDeliverablesMessage>(this, (_, m) => ShowDeliverables(m));
        messenger.Register<OpenPreCertMessage>(this, (_, m) => ShowPreCert(m));
        messenger.Register<OpenHelpfilesMessage>(this, (_, m) => ShowHelpfiles(m));
        messenger.Register<BackToProjectsMessage>(this, (_, _) => CurrentViewModel = projects);
    }

    private void ShowDeliverables(OpenDeliverablesMessage message)
    {
        var vm = _services.GetRequiredService<DeliverablesViewModel>();
        CurrentViewModel = vm;
        _ = vm.LoadAsync(message.ProjectId, message.GameId, message.ProjectName);
    }

    private void ShowPreCert(OpenPreCertMessage message)
    {
        var vm = _services.GetRequiredService<PreCertViewModel>();
        CurrentViewModel = vm;
        _ = vm.LoadAsync(message.ProjectId, message.ProjectName);
    }

    private void ShowHelpfiles(OpenHelpfilesMessage message)
    {
        var vm = _services.GetRequiredService<HelpfilesViewModel>();
        CurrentViewModel = vm;
        _ = vm.LoadAsync(message.GameId, message.GameName);
    }
}
