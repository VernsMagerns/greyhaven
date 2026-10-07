using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Core.Compatibility;
using GreyHaven.Reports;

namespace GreyHaven.Presentation.ViewModels;

/// <summary>A way to install on the "Choose how to install" step. Erasing needs a confirmation tick first.</summary>
public sealed partial class InstallOptionViewModel(InstallOption option, WalkthroughViewModel owner) : ObservableObject
{
    public string Id { get; } = option.Id;
    public string Title { get; } = option.Title;
    public IReadOnlyList<string> Lines { get; } = option.Lines;
    public bool Possible { get; } = option.Possible;
    public string? ConfirmText { get; } = option.ConfirmText;
    public IReadOnlyList<Block> Steps { get; } = option.Steps;
    public string AccessibleName => $"{Title}. {string.Join(" ", Lines)}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSteps))]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        // Any change of choice clears the erase confirmation, so it's always ticked fresh.
        Confirmed = false;
        if (!value) return;
        foreach (var o in owner.InstallOptions) if (o != this) o.IsSelected = false;
    }

    /// <summary>Ticked "I understand everything on this drive will be erased...".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSteps))]
    public partial bool Confirmed { get; set; }

    public bool NeedsConfirmation => ConfirmText is not null;
    /// <summary>The steps appear once chosen, and for erasing only after the confirmation.</summary>
    public bool ShowSteps => IsSelected && (ConfirmText is null || Confirmed);
}

/// <summary>
/// The guided "What's next" walkthrough: one step per screen with progress, Back/Next and a
/// "What could go wrong?" tip. Content comes from <see cref="WalkthroughBuilder"/>, the same
/// steps the saved report prints.
/// </summary>
public sealed partial class WalkthroughViewModel : ObservableObject
{
    private readonly IReadOnlyList<WalkStep> _steps;
    private readonly Action _backToResults;

    public WalkthroughViewModel(CompatibilityReport report, Action backToResults)
    {
        _steps = WalkthroughBuilder.Build(report);
        _backToResults = backToResults;
        StepTitles = _steps.Select((s, i) => new StepLink(i, s.Title, this)).ToList();
        Go(0);
    }

    public int Count => _steps.Count;
    public IReadOnlyList<StepLink> StepTitles { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(IsLast), nameof(NextLabel))]
    public partial int Index { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<Block> Body { get; private set; } = [];

    [ObservableProperty]
    public partial string? Tip { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<InstallOptionViewModel> InstallOptions { get; private set; } = [];

    /// <summary>"Step 3 of 9"; also announced to screen readers with the title.</summary>
    [ObservableProperty]
    public partial string StepCountText { get; private set; } = "";

    [ObservableProperty]
    public partial string Announcement { get; private set; } = "";

    /// <summary>One segment per step: done, now, or still to come.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Progress { get; private set; } = [];

    public bool CanGoBack => Index > 0;
    public bool IsLast => Index == Count - 1;
    public string NextLabel => IsLast ? "Finish" : $"Next: {_steps[Index + 1].Title} →";

    public void Go(int index)
    {
        Index = Math.Clamp(index, 0, Count - 1);
        var s = _steps[Index];
        Title = s.Title;
        Body = s.Body;
        Tip = s.Tip;
        InstallOptions = s.Options.Select(o => new InstallOptionViewModel(o, this)).ToList();
        StepCountText = $"Step {Index + 1} of {Count}";
        Announcement = $"{StepCountText}: {s.Title}";
        Progress = Enumerable.Range(0, Count).Select(i => i < Index ? "done" : i == Index ? "now" : "next").ToList();
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLast) _backToResults();
        else Go(Index + 1);
    }

    [RelayCommand]
    private void Back() => Go(Index - 1);

    [RelayCommand]
    private void BackToResults() => _backToResults();

    public sealed class StepLink(int index, string title, WalkthroughViewModel owner)
    {
        public string Title { get; } = $"{index + 1}. {title}";
        public IRelayCommand GoCommand { get; } = new RelayCommand(() => owner.Go(index));
    }
}
