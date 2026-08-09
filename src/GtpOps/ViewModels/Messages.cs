namespace GtpOps.ViewModels;

/// <summary>Open the deliverables validator for a project.</summary>
public sealed record OpenDeliverablesMessage(int ProjectId, int GameId, string? ProjectName);

/// <summary>Open the pre-certification readiness view for a project.</summary>
public sealed record OpenPreCertMessage(int ProjectId, string? ProjectName);

/// <summary>Open the helpfile validation view for a game.</summary>
public sealed record OpenHelpfilesMessage(int GameId, string? GameName);

/// <summary>Return to the projects browser.</summary>
public sealed record BackToProjectsMessage;
