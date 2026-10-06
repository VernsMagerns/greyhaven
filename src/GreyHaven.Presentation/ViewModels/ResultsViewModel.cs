using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;

namespace GreyHaven.Presentation.ViewModels;

public enum NoticeKind { Info, Warning }

public sealed class NoticeViewModel(NoticeKind kind, string title, string message, LinkViewModel? link = null)
{
    public NoticeKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Message { get; } = message;
    public LinkViewModel? Link { get; } = link;
    public string AccessibleName => $"{Title}. {Message}";
}

public sealed record CaveatViewModel(string Title, string Why, string WhatToTest);

/// <summary>A distro's score card. Selecting it shows that distro's ratings.</summary>
public sealed partial class DistroCardViewModel(DistroSummary summary, ResultsViewModel owner) : ObservableObject
{
    public string DistroId { get; } = summary.DistroId;
    public string Name { get; } = summary.DisplayName;
    public string ScoreText { get; } = $"{summary.Score.Score}";
    public string BandText { get; } = RatingDisplay.Band(summary.Score.Band);
    public string AccessibleName => $"{Name}: {summary.Score.Score} out of 100, {BandText}.{(IsChosen ? " Your chosen distro." : "")}";

    /// <summary>Two-way bound to a radio button; selecting shows this distro's ratings.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) owner.ShowDistro(DistroId);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    [NotifyPropertyChangedFor(nameof(ChosenLabel))]
    public partial bool IsChosen { get; set; }

    public string? ChosenLabel => IsChosen ? "Your choice" : null;
    public string ScoreLine => $"{ScoreText} / 100, {BandText}";
}

/// <summary>One way to switch (Easy or Advanced, or the single recommendation).</summary>
public sealed partial class PathOptionViewModel : ObservableObject
{
    private readonly ResultsViewModel _owner;

    public PathOptionViewModel(RecommendationPath path, bool isDefault, ResultsViewModel owner, IAppServices services)
    {
        _owner = owner;
        Kind = path.Kind;
        Title = path.Title;
        Subtitle = path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot)
            ? "Needs Secure Boot turned off"
            : "Keeps Secure Boot on";
        Paragraph = path.Paragraph;
        IsDefault = isDefault;
        DownloadText = $"You'll download: {path.EditionName}";
        SecureBootNotice = path.SecureBootNotice;
        if (path.SecureBootGuide is { } g)
        {
            // GreyHaven's own guide ships with the app, so it works offline and while the
            // repository is private.
            var target = g.LocalPath is not null && (g.Url is null || !GreyHavenInfo.IsGitHubRepositoryConfigured)
                ? Path.Combine(AppContext.BaseDirectory, g.LocalPath)
                : g.Url;
            Guide = new LinkViewModel(g.FromManufacturer ? $"How to turn off Secure Boot: {g.Title}" : $"How to turn off Secure Boot (GreyHaven guide)", target, services);
        }
    }

    public PathKind Kind { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Paragraph { get; }
    public bool IsDefault { get; }
    public string? DefaultLabel => IsDefault ? "Suggested for most people" : null;
    public string DownloadText { get; }
    public string? SecureBootNotice { get; }
    public LinkViewModel? Guide { get; }
    public string AccessibleName => $"{Title}. {Subtitle}.{(IsDefault ? " Suggested for most people." : "")}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) _owner.ChoosePath(Kind);
    }
}

public sealed record ComponentGroupViewModel(string Heading, IReadOnlyList<ComponentViewModel> Components)
{
    public string AccessibleName => $"{Heading}, {Components.Count} {(Components.Count == 1 ? "part" : "parts")}";
}

/// <summary>Everything on the results screen.</summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly IAppServices _services;

    public ResultsViewModel(CompatibilityReport report, IAppServices services)
    {
        _services = services;
        Report = report;

        RecommendationText = report.Recommendation.Paragraph;
        Paths = report.Recommendation.Paths.Select(p => new PathOptionViewModel(p, p.Kind == report.Recommendation.DefaultPath, this, services)).ToList();
        Distros = report.Distros.Select(d => new DistroCardViewModel(d, this)).ToList();
        Caveats = report.Caveats.Select(c => new CaveatViewModel(c.Title, c.Why, c.WhatToTest)).ToList();
        DataBuiltText = $"Hardware data from {report.Data.BuiltOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}";
        Notices = BuildNotices(report, services);

        var initial = Paths.FirstOrDefault(p => p.Kind == report.SelectedPath);
        if (initial is not null) initial.IsSelected = true; // also selects the distro
        else ShowDistro(report.Distros[0].DistroId);
    }

    [ObservableProperty]
    public partial CompatibilityReport Report { get; private set; }

    public string RecommendationText { get; }
    public bool HasRecommendation => Paths.Count > 0;
    public bool HasPathChoice => Paths.Count > 1;
    public IReadOnlyList<PathOptionViewModel> Paths { get; }
    public IReadOnlyList<DistroCardViewModel> Distros { get; }
    public IReadOnlyList<NoticeViewModel> Notices { get; }
    public IReadOnlyList<CaveatViewModel> Caveats { get; }
    public string DataBuiltText { get; }

    // ---- Selected distro (summary score up top + component list) ----

    [ObservableProperty]
    public partial string SelectedDistroId { get; private set; } = "";

    [ObservableProperty]
    public partial string DistroName { get; private set; } = "";

    /// <summary>Explains the "Essential" and "Important" labels on parts.</summary>
    public string ImportanceKey { get; } =
        "Parts labelled \"Essential\" are ones the PC can't be used properly without, and they count most toward the score. " +
        "\"Important\" parts are ones you'd notice if they didn't work.";

    [ObservableProperty]
    public partial string ScoreText { get; private set; } = "";

    [ObservableProperty]
    public partial string BandText { get; private set; } = "";

    [ObservableProperty]
    public partial string ScoreAccessibleName { get; private set; } = "";

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = "";

    [ObservableProperty]
    public partial string? ScoreNote { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ComponentGroupViewModel> Groups { get; private set; } = [];

    /// <summary>Set when the user is comparing a distro other than the one their path uses.</summary>
    [ObservableProperty]
    public partial string? ComparingText { get; private set; }

    public string? ChosenDistroId => Report.SelectedDistroId;

    /// <summary>Called when a path is selected: carries the choice into the report (fixes, checklist, ISO, export).</summary>
    public void ChoosePath(PathKind kind)
    {
        foreach (var p in Paths) if (p.Kind != kind) p.IsSelected = false;
        Report = Report.WithPath(kind);
        Groups = []; // force the component list to rebuild for the chosen distro
        OnPropertyChanged(nameof(ChosenDistroId));
        foreach (var d in Distros) d.IsChosen = d.DistroId == Report.SelectedDistroId;
        ShowDistro(Report.SelectedDistroId!);
    }

    public void ShowDistro(string distroId)
    {
        var summary = Report.Distros.First(d => d.DistroId == distroId);
        if (SelectedDistroId == distroId && Groups.Count > 0) return;
        SelectedDistroId = distroId;
        foreach (var d in Distros) d.IsSelected = d.DistroId == distroId;

        DistroName = summary.DisplayName;
        ScoreText = summary.Score.Score.ToString(CultureInfo.InvariantCulture);
        BandText = RatingDisplay.Band(summary.Score.Band);
        ScoreAccessibleName = $"Score for {summary.DisplayName}: {summary.Score.Score} out of 100, {BandText}.";

        var ratings = Report.Components.Select(c => c.For(distroId).Rating).ToList();
        SummaryText = Summarise(ratings);
        ScoreNote = summary.Score.CapReason ?? (summary.Score.Incomplete ? "Some essential parts couldn't be checked, so this score is incomplete." : null);

        var vms = Report.Components
            .OrderBy(c => c.Importance)
            .ThenBy(c => c.Component.Category)
            .Select(c => new ComponentViewModel(c, distroId, Report, _services))
            .ToList();
        var attention = vms.Where(v => v.Rating != Rating.WorksOutOfTheBox).ToList();
        var works = vms.Where(v => v.Rating == Rating.WorksOutOfTheBox).ToList();
        Groups = new[]
        {
            new ComponentGroupViewModel("Needs your attention", attention),
            new ComponentGroupViewModel("Works out of the box", works),
        }.Where(g => g.Components.Count > 0).ToList();

        ComparingText = Report.SelectedDistroId is { } chosen && chosen != distroId
            ? $"You're comparing {summary.DisplayName}. Your chosen path uses {Report.Distros.First(d => d.DistroId == chosen).DisplayName}."
            : null;
    }

    [RelayCommand]
    private void ShowChosenDistro()
    {
        if (Report.SelectedDistroId is { } id) ShowDistro(id);
    }

    internal static string Summarise(IReadOnlyList<Rating> ratings)
    {
        static string Count(int n, string one, string many) => n == 1 ? $"1 part {one}" : $"{n} parts {many}";
        int N(Rating r) => ratings.Count(x => x == r);

        var parts = new List<string>();
        if (N(Rating.WorksOutOfTheBox) is > 0 and var w) parts.Add(Count(w, "works out of the box", "work out of the box"));
        if (N(Rating.WorksWithExtraDriver) is > 0 and var e) parts.Add(Count(e, "needs an extra step", "need an extra step"));
        if (N(Rating.Partial) is > 0 and var p) parts.Add(Count(p, "partly works", "partly work"));
        if (N(Rating.Unsupported) is > 0 and var u) parts.Add(Count(u, "doesn't work", "don't work"));
        if (N(Rating.Unknown) is > 0 and var k) parts.Add(Count(k, "couldn't be checked", "couldn't be checked"));
        return parts.Count switch
        {
            0 => "",
            1 => parts[0] + ".",
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1] + ".",
        };
    }

    private static IReadOnlyList<NoticeViewModel> BuildNotices(CompatibilityReport report, IAppServices services)
    {
        var notices = new List<NoticeViewModel>();
        if (services.DemoSample is { } sample)
            notices.Add(new(NoticeKind.Info, "Demo mode", $"This report uses a sample PC (\"{sample}\"), not your hardware."));
        if (report.Data.Notice is { } stale)
            notices.Add(new(NoticeKind.Warning, "Check for a GreyHaven update", stale));
        if (report.Profile.System.IsArm)
            notices.Add(new(NoticeKind.Warning, "This PC has an ARM processor", "Linux support for ARM Windows PCs is limited, and most parts can't be checked from here."));
        var scanNotes = report.Profile.Warnings.Where(w => !w.Contains("ARM processor", StringComparison.Ordinal)).ToList();
        if (scanNotes.Count > 0)
            notices.Add(new(NoticeKind.Info, "Some details couldn't be read", string.Join(" ", scanNotes)));
        // Data notes (e.g. "online check unavailable") appear once here, never per component.
        foreach (var note in report.ContextNotes)
            notices.Add(new(NoticeKind.Info, "About Windows 10", note.Text, new LinkViewModel(note.Source.Title, note.Source.Url, services)));
        foreach (var note in report.DataNotes)
            notices.Add(new(NoticeKind.Info, "About this report", note));
        return notices;
    }
}
