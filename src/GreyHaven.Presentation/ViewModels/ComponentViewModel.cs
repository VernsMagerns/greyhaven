using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Presentation.ViewModels;

/// <summary>A link to a source or guide. Sources without a URL are shown as plain text.</summary>
public sealed class LinkViewModel(string title, string? target, IAppServices services)
{
    public string Title { get; } = title;
    public string? Target { get; } = target;
    public bool IsLink => Target is not null;
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => { if (target is not null) services.Open(target); }, () => target is not null);
}

public sealed class CommandLineViewModel(string text, IAppServices services)
{
    public string Text { get; } = text;
    public string AccessibleName => $"Command: {Text}";
    public IRelayCommand CopyCommand { get; } = new RelayCommand(() => services.CopyToClipboard(text));
}

public sealed record DetailRow(string Label, string Value);

/// <summary>One part of the PC, as rated for one distro. Main view first; technical details one click away.</summary>
public sealed partial class ComponentViewModel : ObservableObject
{
    public ComponentViewModel(RatedComponent rated, string distroId, CompatibilityReport report, IAppServices services)
    {
        var c = rated.Component;
        var r = rated.For(distroId);
        Rating = r.Rating;

        Title = Labels.Of(c.Category);
        Name = c.Count > 1 ? $"{c.Name} (×{c.Count})" : c.Name;
        RatingLabel = RatingDisplay.Label(r.Rating);
        Glyph = RatingDisplay.Glyph(r.Rating);
        ColourKey = RatingDisplay.ColourKey(r.Rating);
        IsEssential = rated.Importance == Importance.Critical;
        Explanation = r.Explanation;
        ApproximationNote = r.ApproximationNote;
        ConfidenceLabel = $"How sure GreyHaven is: {RatingDisplay.Confidence(r.Confidence)}";
        WeakDataLabel = r.IsWeakData ? "Based on limited data" : null;
        AccessibleName = $"{Title}: {RatingLabel}.{(IsEssential ? " Essential part." : "")} {Name}";

        if (r.Fix is { } fix)
        {
            FixTitle = $"What to do: {fix.Driver}";
            FixSteps = fix.Steps;
            FixCommands = fix.Commands.Select(cmd => new CommandLineViewModel(cmd, services)).ToList();
            CopyAllCommandsCommand = new RelayCommand(() => services.CopyToClipboard(string.Join(Environment.NewLine, fix.Commands)), () => fix.Commands.Count > 0);
            FixNote = fix.Note;
            SecureBootAdvice = fix.SecureBootAdvice;
            NeedsInternetText = fix.NeedsInternet ? "This step needs an internet connection." : null;
        }

        Sources = r.Sources.Select(s => new LinkViewModel(s.Title, s.Url, services)).ToList();
        LiveUsbTests = report.Caveats.Where(cv => rated.CaveatIds.Contains(cv.Id)).Select(cv => cv.Title).ToList();

        var details = new List<DetailRow>
        {
            new("Rating", RatingDisplay.OfficialName(r.Rating)),
            new("Confidence", RatingDisplay.Confidence(r.Confidence)),
        };
        if (c.Id is { } id)
        {
            details.Add(new("Device ID", id.Key));
            if (id.HasSubsystem) details.Add(new("Subsystem", $"{id.SubVendor}:{id.SubDevice}"));
            if (id.ClassCode is not null) details.Add(new("PCI class", id.ClassCode));
        }
        if (c.Cpu is { } cpu) details.Add(new("Processor", $"{cpu.Vendor}, family {cpu.Family}, model {cpu.Model}"));
        if (r.LinuxDriver is not null) details.Add(new("Linux driver", r.LinuxDriver));
        if (c.WindowsDriver is not null) details.Add(new("Windows driver", c.WindowsDriver));
        if (c.WindowsProblemCode is > 0) details.Add(new("Windows status", c.WindowsProblemCode == 28 ? "No Windows driver installed" : $"Problem code {c.WindowsProblemCode}"));
        details.Add(new("Importance", rated.Importance.ToString()));
        TechnicalDetails = details;
    }

    public Rating Rating { get; }
    public string Title { get; }
    public string Name { get; }
    public string RatingLabel { get; }
    public string Glyph { get; }
    /// <summary>Palette name of the rating colour (always paired with <see cref="Glyph"/> and <see cref="RatingLabel"/>).</summary>
    public string ColourKey { get; }
    public bool IsEssential { get; }
    public string Explanation { get; }
    public string? ApproximationNote { get; }
    public string ConfidenceLabel { get; }
    public string? WeakDataLabel { get; }
    public string AccessibleName { get; }

    public bool HasFix => FixTitle is not null;
    public string? FixTitle { get; }
    public IReadOnlyList<string> FixSteps { get; } = [];
    public IReadOnlyList<CommandLineViewModel> FixCommands { get; } = [];
    public IRelayCommand? CopyAllCommandsCommand { get; }
    public string? FixNote { get; }
    public string? SecureBootAdvice { get; }
    public string? NeedsInternetText { get; }

    public IReadOnlyList<LinkViewModel> Sources { get; }
    public IReadOnlyList<string> LiveUsbTests { get; }
    public IReadOnlyList<DetailRow> TechnicalDetails { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool ShowTechnicalDetails { get; set; }
}
