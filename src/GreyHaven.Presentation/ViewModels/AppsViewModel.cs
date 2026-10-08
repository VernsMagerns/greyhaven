using CommunityToolkit.Mvvm.ComponentModel;
using GreyHaven.Core.Apps;

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
        (Glyph, ColourKey) = app.Verdict switch
        {
            AppVerdict.Works or AppVerdict.NotNeeded => ("", "Works"),
            AppVerdict.WorksWithLayer or AppVerdict.UseWeb => ("", "ExtraStep"),
            AppVerdict.WontWork => ("", "Unsupported"),
            _ => ("", "Unknown"),
        };
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
