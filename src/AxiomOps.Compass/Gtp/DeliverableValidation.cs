namespace AxiomOps.Compass.Gtp;

/// <summary>The three certification stages, in release order.</summary>
public enum DeliverableStage
{
    Testing,
    SignOff,
    Certification,
}

/// <summary>Coverage of one deliverable for a single variant × market.</summary>
public sealed class DeliverableCoverage
{
    public required string Variant { get; init; }
    public required string Market { get; init; }
    public required bool IsMet { get; init; }
}

/// <summary>A deliverable with its status and full variant×market coverage.</summary>
public sealed class DeliverableItem
{
    public required int DeliverableId { get; init; }
    public required int DeliverableTypeId { get; init; }
    public required string DeliverableType { get; init; }
    public required DeliverableStage Stage { get; init; }
    public required bool IsOptional { get; init; }
    public required bool IsMet { get; init; }
    public required bool CanAutoGenerate { get; init; }
    public required int FileCount { get; init; }

    /// <summary>User marked this type as "not mandatory for us" (personal override).</summary>
    public required bool IsDiscarded { get; init; }

    /// <summary>User marked this type as their (backend) responsibility.</summary>
    public required bool IsBackend { get; init; }

    /// <summary>Blocks a release: GTP-required, not discarded, not met.</summary>
    public bool IsBlocker => !IsOptional && !IsDiscarded && !IsMet;

    /// <summary>Counts toward the required tally: GTP-required and not discarded.</summary>
    public bool IsEffectivelyRequired => !IsOptional && !IsDiscarded;

    /// <summary>Per variant×market rows; empty for "general" (non-per-market) deliverables.</summary>
    public required IReadOnlyList<DeliverableCoverage> Coverage { get; init; }

    public bool HasMarketCoverage => Coverage.Count > 0;

    /// <summary>Only the variant×market holes ("V94 → Italy, West Virginia"); empty when general or fully met.</summary>
    public IReadOnlyList<string> MarketGaps =>
    [
        .. Coverage
            .Where(c => !c.IsMet)
            .GroupBy(c => c.Variant)
            .Select(g => $"{g.Key} → {string.Join(", ", g.Select(c => c.Market))}"),
    ];
}

/// <summary>Per-stage roll-up: how many required deliverables are met.</summary>
public sealed class StageSummary
{
    public required DeliverableStage Stage { get; init; }
    public required int RequiredTotal { get; init; }
    public required int RequiredMet { get; init; }
    public required int OptionalTotal { get; init; }
    public required int OptionalMet { get; init; }

    public bool IsComplete => RequiredMet >= RequiredTotal;
    public bool HasRequirements => RequiredTotal > 0;
}

/// <summary>Full validation result for a project's V2 deliverables.</summary>
public sealed class DeliverableValidationResult
{
    public required IReadOnlyList<StageSummary> Stages { get; init; }

    /// <summary>Every deliverable of the project, with its status and coverage.</summary>
    public required IReadOnlyList<DeliverableItem> Items { get; init; }

    /// <summary>Blockers: GTP-required, not discarded, not met.</summary>
    public IReadOnlyList<DeliverableItem> MissingRequired =>
        [.. Items.Where(i => i.IsBlocker)];

    /// <summary>Deliverables that are met (and not discarded).</summary>
    public IReadOnlyList<DeliverableItem> Loaded =>
        [.. Items.Where(i => i.IsMet && !i.IsDiscarded)];

    /// <summary>Optional deliverables not yet met (advisory, non-blocking).</summary>
    public IReadOnlyList<DeliverableItem> OptionalPending =>
        [.. Items.Where(i => i.IsOptional && !i.IsMet && !i.IsDiscarded)];

    /// <summary>Deliverables the user marked as not-mandatory-for-us.</summary>
    public IReadOnlyList<DeliverableItem> Discarded =>
        [.. Items.Where(i => i.IsDiscarded)];

    /// <summary>Deliverables the user owns (backend), met and pending mixed.</summary>
    public IReadOnlyList<DeliverableItem> Backend =>
        [.. Items.Where(i => i.IsBackend)];

    /// <summary>My (backend) deliverables still pending: mine, effectively required, not met.</summary>
    public IReadOnlyList<DeliverableItem> BackendMissing =>
        [.. Items.Where(i => i.IsBackend && i.IsBlocker)];

    /// <summary>My (backend) deliverables already loaded.</summary>
    public IReadOnlyList<DeliverableItem> BackendLoaded =>
        [.. Items.Where(i => i.IsBackend && i.IsMet)];

    public int TotalDeliverables => Items.Count;
    public int TotalFiles { get; init; }

    /// <summary>Every required deliverable across every stage is met.</summary>
    public bool AllRequiredMet => Stages.All(s => s.IsComplete);

    /// <summary>
    /// The V2 system returned no deliverables at all for this project (empty
    /// array, e.g. legacy projects fully managed in V1). Distinct from a fresh
    /// V2 project that has the full requirement list with nothing loaded yet —
    /// that one shows every deliverable as pending, not an empty state.
    /// </summary>
    public bool HasNoDeliverables => Items.Count == 0;
}

/// <summary>Stage circle for the stepper.</summary>
public static class DeliverableStageNames
{
    public static string Display(DeliverableStage stage) => stage switch
    {
        DeliverableStage.SignOff => "Sign Off",
        DeliverableStage.Certification => "Certification",
        _ => "Testing",
    };
}

/// <summary>
/// Turns the raw V2 deliverables into a release-readiness report. Uses
/// <see cref="GtpDeliverableRequirement.IsRequirementMet"/> as the source of
/// truth (submission-status is ignored — it reports complete even when it isn't).
/// </summary>
public static class DeliverableValidation
{
    /// <summary>
    /// Builds the report. <paramref name="discardedTypeIds"/> are the deliverable
    /// type ids the user marked as not-mandatory-for-us; they drop out of the
    /// required tally and the blockers, moving to the "descartados" bucket.
    /// </summary>
    public static DeliverableValidationResult Validate(
        IEnumerable<GtpDeliverable> deliverables,
        IReadOnlySet<int>? discardedTypeIds = null,
        IReadOnlySet<int>? backendTypeIds = null)
    {
        var discarded = discardedTypeIds ?? new HashSet<int>();
        var backend = backendTypeIds ?? new HashSet<int>();
        var list = deliverables.ToList();

        var items = list
            .Select(d => ToItem(d, discarded.Contains(d.DeliverableTypeId), backend.Contains(d.DeliverableTypeId)))
            .OrderBy(i => i.Stage)
            .ThenByDescending(i => i.IsEffectivelyRequired)
            .ThenBy(i => i.DeliverableType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The traffic light reflects the EFFECTIVE required set (post-discard).
        var stages = Enum.GetValues<DeliverableStage>()
            .Select(stage =>
            {
                var required = items.Where(i => i.Stage == stage && i.IsEffectivelyRequired).ToList();
                var optional = items.Where(i => i.Stage == stage && i.IsOptional && !i.IsDiscarded).ToList();

                return new StageSummary
                {
                    Stage = stage,
                    RequiredTotal = required.Count,
                    RequiredMet = required.Count(i => i.IsMet),
                    OptionalTotal = optional.Count,
                    OptionalMet = optional.Count(i => i.IsMet),
                };
            })
            .ToList();

        return new DeliverableValidationResult
        {
            Stages = stages,
            Items = items,
            TotalFiles = list.Sum(d => d.Files?.Count ?? 0),
        };
    }

    private static bool IsMet(GtpDeliverable d) => d.Requirement?.IsRequirementMet ?? false;

    private static DeliverableItem ToItem(GtpDeliverable d, bool isDiscarded, bool isBackend) => new()
    {
        DeliverableId = d.DeliverableId,
        DeliverableTypeId = d.DeliverableTypeId,
        DeliverableType = d.DeliverableType ?? $"Deliverable {d.DeliverableId}",
        Stage = ParseStage(d.SubmissionName),
        IsOptional = d.IsOptional,
        IsMet = IsMet(d),
        CanAutoGenerate = d.CanAutoGenerate,
        FileCount = d.Files?.Count ?? 0,
        IsDiscarded = isDiscarded,
        IsBackend = isBackend,
        Coverage = BuildCoverage(d.Requirement),
    };

    /// <summary>Flattens the requirement tree into per variant×market coverage rows.</summary>
    private static List<DeliverableCoverage> BuildCoverage(GtpDeliverableRequirement? requirement)
    {
        var coverage = new List<DeliverableCoverage>();
        if (requirement?.GamingSystemRequirements is not { } systems)
        {
            return coverage;
        }

        foreach (var variant in systems.SelectMany(s => s.PayoutVariantRequirements ?? []))
        {
            foreach (var market in variant.MarketRequirements ?? [])
            {
                coverage.Add(new DeliverableCoverage
                {
                    Variant = variant.PayoutShortName ?? "?",
                    Market = market.MarketLabel ?? $"Market {market.MarketId}",
                    IsMet = market.IsRequirementMet,
                });
            }
        }

        return coverage;
    }

    private static DeliverableStage ParseStage(string? name) => name switch
    {
        "SignOff" => DeliverableStage.SignOff,
        "Certification" => DeliverableStage.Certification,
        _ => DeliverableStage.Testing,
    };
}
