namespace AxiomOps.UI.ViewModels;

/// <summary>Sent by the selector after the api-key was validated against the environment.</summary>
public sealed record EnvironmentConnectedMessage(string InternalName);

/// <summary>Sent by the dashboard when the user wants to pick another environment.</summary>
public sealed record ChangeEnvironmentRequestedMessage;

/// <summary>Sent by the dashboard to open the bulk bet-settings tool.</summary>
public sealed record OpenBetSettingsToolMessage;

/// <summary>Sent by the dashboard to open the users browser.</summary>
public sealed record OpenUsersToolMessage;

/// <summary>Sent by the dashboard to open the games install-diagnostics view.</summary>
public sealed record OpenGamesToolMessage;

/// <summary>Sent by the dashboard to open the launch module.</summary>
public sealed record OpenLaunchToolMessage;

/// <summary>Sent by the dashboard to open the file manager.</summary>
public sealed record OpenFilesToolMessage;

/// <summary>Sent by the dashboard to open the log viewer.</summary>
public sealed record OpenLogsToolMessage;

/// <summary>Sent by the dashboard to open the testdata manager.</summary>
public sealed record OpenTestDataToolMessage;

/// <summary>Sent by the users screen to inspect a user's plays (event/game/stats data).</summary>
public sealed record OpenGameEventDataMessage(int UserId, string? LoginName);

/// <summary>Sent by the users screen to open the account-creation form.</summary>
public sealed record OpenCreateUserMessage;

/// <summary>Sent by the dashboard to open the manual game-deploy module.</summary>
public sealed record OpenDeployGameMessage;

/// <summary>Sent by the dashboard to open the play-repository generator.</summary>
public sealed record OpenPlayRepositoryToolMessage;

/// <summary>Sent by the dashboard to open the Free Games offer form.</summary>
public sealed record OpenFreeGamesToolMessage;

/// <summary>Sent by a tool view to go back to the dashboard.</summary>
public sealed record BackToDashboardMessage;
