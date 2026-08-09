namespace AxiomOps.Compass.Gtp;

public enum HelpfileCheckKind
{
    Blocking,
    Warning,
    Info,
}

/// <summary>One line of the helpfile readiness checklist.</summary>
public sealed class HelpfileCheck
{
    public required string Label { get; init; }
    public required bool Passed { get; init; }
    public required HelpfileCheckKind Kind { get; init; }

    /// <summary>Extra detail, e.g. the list of missing markets.</summary>
    public string? Detail { get; init; }
}

/// <summary>Release-readiness verdict for a single helpfile.</summary>
public sealed class HelpfileReport
{
    public required GtpHelpfile Helpfile { get; init; }
    public required IReadOnlyList<HelpfileCheck> Checks { get; init; }
    public required bool IsReadyForSubmission { get; init; }
    public required int MissingTranslations { get; init; }

    /// <summary>Our own content-quality findings (null when the strings weren't loaded).</summary>
    public HelpfileContentAnalysis? Content { get; init; }

    /// <summary>Ready to release: GTP says ready, no missing translations, and content rules pass.</summary>
    public bool IsReleasable =>
        IsReadyForSubmission && MissingTranslations == 0 && (Content?.IsClean ?? true);

    /// <summary>Blocking checks that failed (what stops the release).</summary>
    public IReadOnlyList<HelpfileCheck> Blockers =>
        [.. Checks.Where(c => c.Kind == HelpfileCheckKind.Blocking && !c.Passed)];

    /// <summary>Every content finding, blocking ones first — the drill-down list.</summary>
    public IReadOnlyList<HelpfileContentIssue> ContentIssues => Content is null
        ? []
        : [.. Content.MissingPeriod, .. Content.DuplicatesInTopic, .. Content.DuplicatesAcrossTopics];

    public bool HasContentIssues => ContentIssues.Count > 0;
}

/// <summary>Turns the raw easy-help validation into a release-readiness checklist.</summary>
public static class HelpfileReadiness
{
    public static HelpfileReport Evaluate(
        GtpHelpfile helpfile,
        GtpHelpfileValidation? validation,
        int missingTranslations,
        HelpfileContentAnalysis? content = null)
    {
        var v = validation ?? new GtpHelpfileValidation();
        var missingMarkets = v.MissingMarketsFromPreviousVersion ?? [];

        List<HelpfileCheck> checks =
        [
            new() { Label = "Mercados válidos", Passed = v.IsMarketsValid, Kind = HelpfileCheckKind.Blocking },
            new() { Label = "Mercado DotCom válido", Passed = v.IsDotcomMarketValid, Kind = HelpfileCheckKind.Blocking },
            new() { Label = "Idiomas válidos", Passed = v.IsLanguagesValid, Kind = HelpfileCheckKind.Blocking },
            new() { Label = "Topics y strings válidos", Passed = v.IsTopicsAndStringsValid, Kind = HelpfileCheckKind.Blocking },
            new() { Label = "Atributos válidos", Passed = v.IsAttributesValid, Kind = HelpfileCheckKind.Blocking },
            new() { Label = "Juego válido", Passed = v.IsGameValid, Kind = HelpfileCheckKind.Blocking },
            new()
            {
                Label = "Sin mercados perdidos vs versión anterior",
                Passed = missingMarkets.Count == 0,
                Kind = HelpfileCheckKind.Blocking,
                Detail = missingMarkets.Count == 0 ? null : $"Perdidos: {string.Join(", ", missingMarkets)}",
            },
            new()
            {
                Label = "Sin traducciones faltantes",
                Passed = missingTranslations == 0,
                Kind = HelpfileCheckKind.Blocking,
                Detail = missingTranslations == 0 ? null : $"{missingTranslations} traducción(es) faltante(s)",
            },
            new() { Label = "Enviado (submitted)", Passed = v.IsSubmitted, Kind = HelpfileCheckKind.Info },
            new() { Label = "Sin idiomas archivados", Passed = !v.HasArchivedLanguages, Kind = HelpfileCheckKind.Warning },
            new() { Label = "Sin strings archivados", Passed = !v.HasArchivedStrings, Kind = HelpfileCheckKind.Warning },
        ];

        // Our own content rules (the ones QA checks by hand).
        if (content is not null)
        {
            checks.Insert(0, new HelpfileCheck
            {
                Label = "Todas las frases terminan en punto",
                Passed = content.MissingPeriod.Count == 0,
                Kind = HelpfileCheckKind.Blocking,
                Detail = content.MissingPeriod.Count == 0
                    ? null
                    : $"{content.MissingPeriod.Count} frase(s) sin punto final",
            });

            checks.Insert(1, new HelpfileCheck
            {
                Label = "Sin frases duplicadas dentro de un tópico",
                Passed = content.DuplicatesInTopic.Count == 0,
                Kind = HelpfileCheckKind.Blocking,
                Detail = content.DuplicatesInTopic.Count == 0
                    ? null
                    : $"{content.DuplicatesInTopic.Count} frase(s) repetida(s) en su tópico",
            });

            checks.Add(new HelpfileCheck
            {
                Label = "Frases repetidas entre tópicos",
                Passed = content.DuplicatesAcrossTopics.Count == 0,
                Kind = HelpfileCheckKind.Info,
                Detail = content.DuplicatesAcrossTopics.Count == 0
                    ? null
                    : $"{content.DuplicatesAcrossTopics.Count} frase(s) — suele ser boilerplate intencional",
            });
        }

        return new HelpfileReport
        {
            Helpfile = helpfile,
            Checks = checks,
            IsReadyForSubmission = v.IsReadyForSubmission,
            MissingTranslations = missingTranslations,
            Content = content,
        };
    }
}
