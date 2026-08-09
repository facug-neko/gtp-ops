using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// Shell: owns the current view. Starts on the environment selector and swaps
/// to a fresh dashboard when an environment is connected.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public MainWindowViewModel(IServiceProvider services, IMessenger messenger)
    {
        _services = services;

        var selector = services.GetRequiredService<EnvironmentSelectorViewModel>();
        _currentViewModel = selector;

        messenger.Register<EnvironmentConnectedMessage>(this, (_, _) => ShowDashboard());
        messenger.Register<ChangeEnvironmentRequestedMessage>(this, (_, _) => CurrentViewModel = selector);
        messenger.Register<BackToDashboardMessage>(this, (_, _) => ShowDashboard());
        messenger.Register<OpenBetSettingsToolMessage>(this, (_, _) => ShowBetSettingsTool());
        messenger.Register<OpenUsersToolMessage>(this, (_, _) => ShowUsersTool());
        messenger.Register<OpenGamesToolMessage>(this, (_, _) => ShowGamesTool());
        messenger.Register<OpenLaunchToolMessage>(this, (_, _) => ShowLaunchTool());
        messenger.Register<OpenFilesToolMessage>(this, (_, _) => ShowFilesTool());
        messenger.Register<OpenLogsToolMessage>(this, (_, _) => ShowLogsTool());
        messenger.Register<OpenTestDataToolMessage>(this, (_, _) => ShowTestDataTool());
        messenger.Register<OpenGameEventDataMessage>(this, (_, m) => ShowGameEventData(m));
        messenger.Register<OpenCreateUserMessage>(this, (_, _) => ShowCreateUser());
        messenger.Register<OpenDeployGameMessage>(this, (_, _) => ShowDeployGame());

        // Fire the initial environment load; errors surface inside the selector.
        _ = selector.LoadEnvironmentsCommand.ExecuteAsync(null);
    }

    private void ShowDashboard()
    {
        var dashboard = _services.GetRequiredService<DashboardViewModel>();
        CurrentViewModel = dashboard;
        _ = dashboard.RefreshCommand.ExecuteAsync(null);
    }

    private void ShowBetSettingsTool()
    {
        var tool = _services.GetRequiredService<BulkBetSettingsViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowUsersTool()
    {
        var tool = _services.GetRequiredService<UsersViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowGamesTool()
    {
        var tool = _services.GetRequiredService<GamesViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowLaunchTool()
    {
        var tool = _services.GetRequiredService<LaunchViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowFilesTool()
    {
        var tool = _services.GetRequiredService<FilesViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowLogsTool()
    {
        var tool = _services.GetRequiredService<LogsViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowTestDataTool()
    {
        var tool = _services.GetRequiredService<TestDataViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowGameEventData(OpenGameEventDataMessage message)
    {
        var tool = _services.GetRequiredService<GameEventDataViewModel>();
        CurrentViewModel = tool;
        _ = tool.LoadAsync(message.UserId, message.LoginName);
    }

    private void ShowCreateUser()
    {
        var tool = _services.GetRequiredService<CreateUserViewModel>();
        CurrentViewModel = tool;
        _ = tool.InitializeCommand.ExecuteAsync(null);
    }

    private void ShowDeployGame() =>
        CurrentViewModel = _services.GetRequiredService<DeployGameViewModel>();
}
