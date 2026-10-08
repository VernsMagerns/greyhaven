using CommunityToolkit.Mvvm.ComponentModel;
using GreyHaven.Core.Apps;
using GreyHaven.Ratings;

namespace GreyHaven.Presentation.ViewModels;

/// <summary>One alternative to an app: what it is, how close, and where to get it on the chosen distro.</summary>
public sealed record AlternativeViewModel(string Name, string? What, string Closeness, string Note, string GetText, string? Link, string? Command, IReadOnlyList<string> Hardware);

/// <summary>One of the person's apps in "Your apps".</summary>
public sealed partial class AppViewModel : ObservableObject
{
    private readonly Action<AppViewModel> _mustHaveChanged;

    public AppViewModel(RatedApp app, string distroId, IReadOnlyList<string> hardware, Func<IEnumerable<Core.Hardware.ComponentCategory>, IReadOnlyList<string>> hardwareFor, bool mustHave, Action<AppViewModel> mustHaveChanged)
    {
        App = app;
        _mustHaveChanged = mustHaveChanged;
        Name = app.Name;
        Verdict = AppWords.Verdict(app.Verdict) + (app.Layer is { } l ? $" ({l})" : "");
        (Glyph, ColourKey) = Look(app.Verdict);
        Summary = app.Summary;
        if (app.Linux is { } linux && app.Verdict == AppVerdict.Works)
        {
            var get = LinuxAppInstalls.For(linux, distroId);
            GetText = $"Get it: {get.Where}"; GetLink = get.Link; Command = get.Command;
        }
        WebUrl = app.WebUrl;
        Hardware = hardware;
        Alternatives = app.Alternatives.Select(a =>
        {
            var get = a.App is { } la ? LinuxAppInstalls.For(la, distroId) : new LinuxAppInstall("Open it in your web browser", a.WebUrl, null);
            var close = a.Closeness switch { "same" => "Does the same job", "close" => "Very close", _ => "Does part of it" };
            return new AlternativeViewModel(a.App?.Name ?? a.WebName ?? "", a.App?.What, close, a.Note, $"Get it: {get.Where}", get.Link, get.Command, hardwareFor(a.Uses));
        }).ToList();
        Source = app.Sources.FirstOrDefault();
        IsMustHave = mustHave;
    }

    /// <summary>The icon and colour for a verdict (also used by "Check a game").</summary>
    public static (string Glyph, string ColourKey) Look(AppVerdict verdict) => verdict switch
    {
        AppVerdict.Works or AppVerdict.NotNeeded => ("", "Works"),
        AppVerdict.WorksWithLayer or AppVerdict.UseWeb => ("", "ExtraStep"),
        AppVerdict.WontWork => ("", "Unsupported"),
        _ => ("", "Unknown"),
    };

    public RatedApp App { get; }
    public string Name { get; }
    public string Verdict { get; }
    public string Glyph { get; }
    public string ColourKey { get; }
    public string Summary { get; }
    public string? GetText { get; }
    public string? GetLink { get; }
    public string? Command { get; }
    public string? WebUrl { get; }
    public IReadOnlyList<string> Hardware { get; }
    public IReadOnlyList<AlternativeViewModel> Alternatives { get; }
    public Core.Compatibility.Source? Source { get; }
    public string AccessibleName => $"{Name}: {Verdict}. {Summary}";
    public string MustHaveLabel => $"I need {Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MustHaveWarning))]
    public partial bool IsMustHave { get; set; }

    partial void OnIsMustHaveChanged(bool value) => _mustHaveChanged(this);

    public string? MustHaveWarning => IsMustHave ? AppWords.MustHaveWarning(App) : null;
}

public sealed record AppGroupViewModel(string Name, IReadOnlyList<AppViewModel> Apps);

/// <summary>One game found by "Check a game".</summary>
public sealed class GameResultViewModel(GameMatch match, IAppServices services)
{
    public string Name { get; } = match.Rating.Name;
    public string Verdict { get; } = AppWords.Verdict(match.Rating.Verdict) + (match.Rating.Layer is { } l ? $" ({l})" : "");
    public string Glyph { get; } = AppViewModel.Look(match.Rating.Verdict).Glyph;
    public string ColourKey { get; } = AppViewModel.Look(match.Rating.Verdict).ColourKey;
    public string Summary { get; } = match.Rating.Summary;
    /// <summary>ProtonDB's page for the game (opens in the browser), or the anti-cheat list's page.</summary>
    public LinkViewModel? Link { get; } = match.ProtonDbUrl is { } url ? new LinkViewModel($"{match.Rating.Name} on ProtonDB", url, services)
        : match.Rating.Sources.FirstOrDefault() is { Url: { } u } src ? new LinkViewModel(src.Title, u, services) : null;
    public string AccessibleName => $"{Name}: {Verdict}. {Summary}";
}

/// <summary>
/// "Check a game": type a name, see whether it runs on Linux, from ProtonDB's report dump and
/// Are We Anti-Cheat Yet (bundled with GreyHaven; nothing is sent anywhere).
/// </summary>
public sealed partial class GameCheckViewModel(IAppServices services) : ObservableObject
{
    [ObservableProperty] public partial string Query { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<GameResultViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string? Message { get; private set; }

    partial void OnQueryChanged(string value)
    {
        var text = value.Trim();
        Results = text.Length < GameSearch.MinimumLength ? [] : services.SearchGames(text).Select(m => new GameResultViewModel(m, services)).ToList();
        Message = text.Length < GameSearch.MinimumLength ? null
            : Results.Count == 0 ? $"No game called \"{text}\" in GreyHaven's list. Check the spelling, or search ProtonDB's website."
            : Results.Count == 1 ? "1 game found." : $"{Results.Count} games found.";
    }

    public string Sources => "From ProtonDB player reports (Open Database License) and Are We Anti-Cheat Yet. Click a game's link to read the reports.";
}
