using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Reports;

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

/// <summary>A distro's comparison card. Selecting it shows that distro's parts.</summary>
public sealed partial class DistroCardViewModel(DistroSummary summary, string shortName, ResultsViewModel owner) : ObservableObject
{
    public string DistroId { get; } = summary.DistroId;
    public string Name { get; } = shortName;
    public string ScoreLine { get; } = $"{summary.Score.Score} out of 100";
    public string AccessibleName => $"{Name}: {summary.Score.Score} out of 100.{(IsChosen ? " Your pick." : "")}";

    /// <summary>Two-way bound to a radio button; selecting shows this distro's parts.</summary>
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

    public string? ChosenLabel => IsChosen ? "Your pick" : null;
}

/// <summary>One way to switch (Easy or Advanced, or the single recommendation): a short card, details behind "Show more".</summary>
public sealed partial class PathOptionViewModel : ObservableObject
{
    private readonly ResultsViewModel _owner;

    public PathOptionViewModel(RecommendationPath path, CompatibilityReport report, bool isDefault, ResultsViewModel owner, IAppServices services)
    {
        _owner = owner;
        Kind = path.Kind;
        Name = ReportBuilder.ShortName(report, path.DistroId);
        Tag = path.Kind == PathKind.Easy ? "Easiest" : "Best fit";
        Lines = ReportBuilder.PathLines(report, path);
        IsDefault = isDefault;
        Paragraph = path.Paragraph;
        SecureBootText = path.SecureBootNotice is null ? null : $"{PlainWords.SecureBoot} {path.SecureBootNotice}";
        YoullGet = ReportBuilder.YoullGet(report, path);
        if (path.SecureBootGuide is { } g)
        {
            // GreyHaven's own guide ships with the app: open that copy, so it works offline
            // and whatever the repository's visibility.
            var target = g.LocalPath is not null
                ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, g.LocalPath.Replace('/', Path.DirectorySeparatorChar)))
                : g.Url;
            var maker = g.FromManufacturer ? $" ({g.Title.Split(" - ")[0]}'s guide)" : " (GreyHaven's guide)";
            Guide = new LinkViewModel($"How to turn off Secure Boot{maker}", target, services);
        }
    }

    public PathKind Kind { get; }
    public string Name { get; }
    public string Tag { get; }
    public IReadOnlyList<string> Lines { get; }
    public bool IsDefault { get; }
    public string? DefaultLabel => IsDefault ? "Suggested" : null;
    public string Paragraph { get; }
    public string? SecureBootText { get; }
    public LinkViewModel? Guide { get; }
    public string YoullGet { get; }
    public string AccessibleName => $"{Name}, {Tag}.{(IsDefault ? " Suggested." : "")} {string.Join(" ", Lines)}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) _owner.ChoosePath(Kind);
    }
}

/// <summary>
/// The results screen, in plain words: headline and score, the version of Linux to use, "What's
/// next", the parts that need attention (working parts collapsed to one line), things to test,
/// a comparison, and saving the report. Details sit behind "Show more".
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly IAppServices _services;

    public ResultsViewModel(CompatibilityReport report, IAppServices services)
    {
        _services = services;
        Report = report;

        Paths = report.Recommendation.Paths.Select(p => new PathOptionViewModel(p, report, p.Kind == report.Recommendation.DefaultPath, this, services)).ToList();
        Distros = report.Distros.Select(d => new DistroCardViewModel(d, ReportBuilder.ShortName(report, d.DistroId), this)).ToList();
        Caveats = report.Caveats.Select(c => new CaveatViewModel(c.Title, c.Why, c.WhatToTest)).ToList();
        CaveatsHeading = report.Caveats.Count == 0 ? null : $"{report.Caveats.Count} {(report.Caveats.Count == 1 ? "thing" : "things")} to test before you install";
        DemoText = services.DemoSample is null ? null : "Demo: this is a sample PC, not yours.";
        Notices = BuildNotices(report);
        if (report.ContextNotes.FirstOrDefault() is { } why)
        {
            WhySwitchText = "Microsoft stopped supporting Windows 10 in October 2025. Security updates now need a special sign-up. Those updates end in October 2027.";
            WhySwitchLink = new LinkViewModel("Read Microsoft's page", why.Source.Url, services);
        }
        var offline = report.DataNotes.Any(n => n.Contains("turned off", StringComparison.Ordinal));
        Footnote = $"Checked with GreyHaven's built-in data from {report.Data.BuiltOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}.{(offline ? " The online check was off." : "")}";

        var initial = Paths.FirstOrDefault(p => p.Kind == report.SelectedPath);
        if (initial is not null) initial.IsSelected = true; // also selects the distro and sets the headline
        else
        {
            UpdateHeadline();
            ShowDistro(report.Distros.OrderByDescending(d => d.Score.Score).First().DistroId);
        }
    }

    [ObservableProperty]
    public partial CompatibilityReport Report { get; private set; }

    public bool HasRecommendation => Paths.Count > 0;
    public bool HasPathChoice => Paths.Count > 1;
    public IReadOnlyList<PathOptionViewModel> Paths { get; }
    public IReadOnlyList<DistroCardViewModel> Distros { get; }
    public IReadOnlyList<NoticeViewModel> Notices { get; }
    public IReadOnlyList<CaveatViewModel> Caveats { get; }
    public string? CaveatsHeading { get; }
    public string? DemoText { get; }
    public string? WhySwitchText { get; }
    public LinkViewModel? WhySwitchLink { get; }
    public string Footnote { get; }
    public string PathsHeading => HasPathChoice ? "Pick your version of Linux" : "Your version of Linux";
    public string? PathsIntro => HasPathChoice ? PlainWords.Distro + " Two fit this PC well." : null;

    // ---- Headline: the chosen path's distro --------------------------------------------------

    [ObservableProperty]
    public partial string Headline { get; private set; } = "";

    [ObservableProperty]
    public partial string? Suggestion { get; private set; }

    [ObservableProperty]
    public partial string HeadlineSummary { get; private set; } = "";

    [ObservableProperty]
    public partial string ScoreText { get; private set; } = "";

    [ObservableProperty]
    public partial string ScoreAccessibleName { get; private set; } = "";

    private void UpdateHeadline()
    {
        var chosen = Report.SelectedDistroId;
        var d = chosen is null ? Report.Distros.OrderByDescending(x => x.Score.Score).First() : Report.Distros.First(x => x.DistroId == chosen);
        Headline = HasRecommendation ? PlainWords.Headline(d.Score.Score) : "We can't suggest a version of Linux yet";
        Suggestion = chosen is null ? null : $"We suggest {ReportBuilder.ShortName(Report, chosen)}.";
        HeadlineSummary = PlainWords.PartsSummary(Report.Components.Select(c => c.For(d.DistroId).Rating).ToList());
        ScoreText = d.Score.Score.ToString(CultureInfo.InvariantCulture);
        ScoreAccessibleName = $"Score: {d.Score.Score} out of 100";
    }

    // ---- Parts: the distro being shown (the chosen one, or one being compared) -------------

    [ObservableProperty]
    public partial string SelectedDistroId { get; private set; } = "";

    [ObservableProperty]
    public partial string PartsHeading { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<ComponentViewModel> AttentionParts { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ComponentViewModel> WorkingParts { get; private set; } = [];

    [ObservableProperty]
    public partial string? WorkingHeading { get; private set; }

    /// <summary>Set when the user is comparing a distro other than the one their path uses.</summary>
    [ObservableProperty]
    public partial string? ComparingText { get; private set; }

    public string? ChosenDistroId => Report.SelectedDistroId;

    /// <summary>Called when a path is selected: carries the choice into the report (steps, ISO, export).</summary>
    public void ChoosePath(PathKind kind)
    {
        foreach (var p in Paths) if (p.Kind != kind) p.IsSelected = false;
        Report = Report.WithPath(kind);
        AttentionParts = [];
        OnPropertyChanged(nameof(ChosenDistroId));
        foreach (var d in Distros) d.IsChosen = d.DistroId == Report.SelectedDistroId;
        UpdateHeadline();
        SavedPath = null;
        SaveStatus = null;
        ShowDistro(Report.SelectedDistroId!, force: true);
    }

    public void ShowDistro(string distroId) => ShowDistro(distroId, force: false);

    private void ShowDistro(string distroId, bool force)
    {
        if (!force && SelectedDistroId == distroId && (AttentionParts.Count + WorkingParts.Count) > 0) return;
        SelectedDistroId = distroId;
        foreach (var d in Distros) d.IsSelected = d.DistroId == distroId;
        var name = ReportBuilder.ShortName(Report, distroId);
        PartsHeading = $"Your PC's parts on {name}";

        var vms = Report.Components
            .OrderBy(c => c.Importance)
            .ThenBy(c => c.Component.Category)
            .Select(c => new ComponentViewModel(c, distroId, Report, _services))
            .ToList();
        AttentionParts = vms.Where(v => v.Rating != Rating.WorksOutOfTheBox).ToList();
        WorkingParts = vms.Where(v => v.Rating == Rating.WorksOutOfTheBox).ToList();
        WorkingHeading = WorkingParts.Count == 0 ? null
            : $"{(AttentionParts.Count == 0 ? "All " : "")}{WorkingParts.Count} {(WorkingParts.Count == 1 ? "part works" : "parts work")} right away. Show them";

        ComparingText = Report.SelectedDistroId is { } chosen && chosen != distroId
            ? $"You're looking at {name}. Your pick is {ReportBuilder.ShortName(Report, chosen)}."
            : null;
    }

    [RelayCommand]
    private void ShowChosenDistro()
    {
        if (Report.SelectedDistroId is { } id) ShowDistro(id);
    }

    // ---- Saving the report ----

    /// <summary>Plain-English result of the last save, announced to screen readers.</summary>
    [ObservableProperty]
    public partial string? SaveStatus { get; private set; }

    [ObservableProperty]
    public partial string? SavedPath { get; private set; }

    /// <summary>Saves the report for the chosen path as a web page, Markdown or JSON (picked by file type in the dialog).</summary>
    [RelayCommand]
    private async Task SaveReportAsync()
    {
        var path = _services.ChooseSaveLocation(ReportExporter.SuggestedFileName(Report, ReportFormat.Html));
        if (path is null) return;
        try
        {
            await ReportExporter.SaveAsync(Report, path, ReportExporter.FormatFromPath(path) ?? ReportFormat.Html);
            SavedPath = path;
            SaveStatus = $"Report saved: {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SavedPath = null;
            SaveStatus = $"The report couldn't be saved there. {ex.Message} Try another folder, such as Documents.";
        }
    }

    [RelayCommand]
    private void ShowSavedReport()
    {
        if (SavedPath is not null) _services.ShowInFolder(SavedPath);
    }

    /// <summary>Only things that need attention stay as notices; demo mode, the Windows 10 note and data notes have their own small places.</summary>
    private static IReadOnlyList<NoticeViewModel> BuildNotices(CompatibilityReport report)
    {
        var notices = new List<NoticeViewModel>();
        if (report.Data.Notice is { } stale)
            notices.Add(new(NoticeKind.Warning, "Check for a GreyHaven update", stale));
        if (report.Profile.System.IsArm)
            notices.Add(new(NoticeKind.Warning, "This PC has an ARM chip", "Linux support for these PCs is still limited, and most parts can't be checked from here."));
        var scanNotes = report.Profile.Warnings.Where(w => !w.Contains("ARM processor", StringComparison.Ordinal)).ToList();
        if (scanNotes.Count > 0)
            notices.Add(new(NoticeKind.Info, "Some details couldn't be read", string.Join(" ", scanNotes)));
        foreach (var note in report.DataNotes.Where(n => !n.Contains("turned off", StringComparison.Ordinal)))
            notices.Add(new(NoticeKind.Info, "About this report", note));
        return notices;
    }
}
