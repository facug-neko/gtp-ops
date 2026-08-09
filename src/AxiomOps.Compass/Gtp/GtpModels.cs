namespace AxiomOps.Compass.Gtp;

/// <summary>A curated catalog game (name → canonical Portal gameId).</summary>
public sealed class GtpGame
{
    public int GameId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int? ClientIdMobile { get; set; }
    public int? ClientIdDesktop { get; set; }

    public override string ToString() => $"{DisplayName} ({GameId})";
}

public sealed class GtpUser
{
    public int UserId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
}

public sealed class GtpGameOrder
{
    public int Id { get; set; }
    public string? OrderName { get; set; }
    public int? StudioId { get; set; }
    public string? DistributionChannel { get; set; }
}

public sealed class GtpParticipant
{
    public GtpUser? User { get; set; }
    public string? Role { get; set; }
}

/// <summary>Project of a game (GET /api/v1/projects/games/{gameId} and /projects/{projectId}).</summary>
public sealed class GtpProject
{
    public int Id { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectType { get; set; }
    public string? ProjectSubType { get; set; }
    public string? ProjectStatus { get; set; }
    public DateTimeOffset? CreatedOn { get; set; }
    public GtpUser? CreatedBy { get; set; }
    public List<string>? DistributionChannels { get; set; }
    public string? Notes { get; set; }
    public GtpGameOrder? GameOrder { get; set; }
    public int? GameOrderId { get; set; }
    public int? SignOffApprovalSheetId { get; set; }
    public string? JiraIssueKey { get; set; }
    public List<GtpParticipant>? GameProjectParticipants { get; set; }
}

public sealed class GtpReleaseTag
{
    public int GameReleaseTagId { get; set; }
    public string? Name { get; set; }
}

public sealed class GtpPayoutVariantRelease
{
    public int Id { get; set; }
    public int PayoutVariantId { get; set; }
    public int? FormDocumentId { get; set; }
}

/// <summary>Release of a project (GET /api/v1/releases/projects/{projectId}).</summary>
public sealed class GtpRelease
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int GameProjectId { get; set; }
    public DateTimeOffset? DateSubmitted { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? Type { get; set; }
    public string? TestCycle { get; set; }
    public string? ReleaseStatus { get; set; }
    public bool Composed { get; set; }
    public int? TestPlanId { get; set; }
    public string? JiraIssueKey { get; set; }
    public GtpUser? User { get; set; }
    public List<GtpReleaseTag>? Tags { get; set; }
    public List<GtpPayoutVariantRelease>? PayoutVariants { get; set; }
}
