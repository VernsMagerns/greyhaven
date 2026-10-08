using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreyHaven.Core.Compatibility;
using GreyHaven.Media;
using GreyHaven.Reports;

namespace GreyHaven.Presentation.ViewModels;

/// <summary>Which screen layout to use for a step.</summary>
public enum JourneyScreen { Choose, Checklist, Tasks, Usb, Guide, Restart, AfterRestart, Blocks }

/// <summary>A tickable item (backup list, final checks before restarting).</summary>
public sealed partial class CheckItemViewModel(string text, Action changed) : ObservableObject
{
    public string Text { get; } = text;
    [ObservableProperty] public partial bool IsChecked { get; set; }
    partial void OnIsCheckedChanged(bool value) => changed();
}

/// <summary>A check line under an install choice: "✓ Enough room…", with words for screen readers.</summary>
public sealed record CheckLineViewModel(string Glyph, string ColourKey, string Text)
{
    public string AccessibleName => $"{(ColourKey == "Works" ? "Good" : ColourKey == "Partial" ? "Note" : "Can't do this yet")}: {Text}";

    public static CheckLineViewModel From(CheckLine c) => c.State switch
    {
        CheckState.Ok => new("✓", "Works", c.Text),
        CheckState.Attention => new("!", "Partial", c.Text),
        _ => new("✗", "Unsupported", c.Text),
    };
}

public sealed partial class InstallChoiceViewModel(InstallChoiceOption option, JourneyViewModel owner) : ObservableObject
{
    public InstallChoice Choice { get; } = option.Choice;
    public string Title { get; } = option.Title;
    public IReadOnlyList<string> Lines { get; } = option.Lines;
    public bool Recommended { get; } = option.Recommended;
    public bool Possible { get; } = option.Possible;
    public IReadOnlyList<CheckLineViewModel> Checks { get; } = option.Checks.Select(CheckLineViewModel.From).ToList();
    public string AccessibleName => $"{Title}{(Recommended ? ", suggested" : "")}. {string.Join(" ", Lines)} {string.Join(" ", Checks.Select(c => c.AccessibleName))}";

    [ObservableProperty] public partial bool IsSelected { get; set; }
    partial void OnIsSelectedChanged(bool value) { if (value) owner.Choose(Choice); }
}

/// <summary>A "Get Windows ready" task: already done, done by GreyHaven with one click (ask first, can undo), or by hand.</summary>
public sealed partial class TaskViewModel : ObservableObject
{
    private readonly PrepTask _task;
    private readonly JourneyViewModel _owner;

    public TaskViewModel(PrepTask task, JourneyViewModel owner)
    {
        _task = task;
        _owner = owner;
        State = task.Done ? "already" : "todo";
    }

    public string Title => _task.Title;
    public string Why => _task.Why;
    public IReadOnlyList<Block> How => _task.How;
    public bool IsOneClick => _task.Action == PrepAction.OneClick;
    public string? ButtonLabel => _task.ButtonLabel;
    public string? Confirm => _task.Confirm;
    public string? Undo => _task.Undo;
    public string WhyPermission => _task.Id == "encryption-off" ? HelperReasons.Why(HelperAction.BitLockerOff) : HelperReasons.Why(HelperAction.FastStartupOff);

    /// <summary>already | todo | confirm | working | done | manual-done</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(ShowButton), nameof(ShowConfirm), nameof(IsWorking))]
    public partial string State { get; private set; }

    [ObservableProperty] public partial string? Message { get; private set; }

    public bool IsDone => State is "already" or "done" or "manual-done";
    public bool ShowButton => IsOneClick && State == "todo";
    public bool ShowConfirm => State == "confirm";
    public bool IsWorking => State == "working";
    public bool CanUndo => State == "done" && _task.Id == "fast-startup";

    [RelayCommand] private void Ask() { State = "confirm"; Message = null; }
    [RelayCommand] private void CancelConfirm() => State = "todo";

    [RelayCommand]
    private async Task DoItAsync()
    {
        State = "working";
        var action = _task.Id == "encryption-off" ? HelperAction.BitLockerOff : HelperAction.FastStartupOff;
        var result = await _owner.Switch.RunElevatedAsync(action, null, null, CancellationToken.None);
        Message = result.Text;
        State = result.Kind == HelperMessage.Ok ? "done" : "todo";
        OnPropertyChanged(nameof(CanUndo));
        _owner.Refresh();
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        State = "working";
        var result = await _owner.Switch.RunElevatedAsync(HelperAction.FastStartupOn, null, null, CancellationToken.None);
        Message = result.Text;
        State = result.Kind == HelperMessage.Ok ? "todo" : "done";
        OnPropertyChanged(nameof(CanUndo));
        _owner.Refresh();
    }

    [ObservableProperty] public partial bool ManuallyDone { get; set; }
    partial void OnManuallyDoneChanged(bool value) { State = value ? "manual-done" : "todo"; _owner.Refresh(); }
}

/// <summary>A USB stick in the list, with whether GreyHaven may use it.</summary>
public sealed partial class StickViewModel(DiskInfo disk, StickCheck check, Action<StickViewModel> chosen) : ObservableObject
{
    public DiskInfo Disk { get; } = disk;
    public StickCheck Check { get; } = check;
    public string Name => $"{Disk.Model}, {UsbSafety.Gb(Disk.SizeBytes)}";
    public string? Problem => Check.Problem;
    public string? Warning => Check.Warning;

    [ObservableProperty] public partial bool IsSelected { get; set; }
    partial void OnIsSelectedChanged(bool value) { if (value) chosen(this); }
}

/// <summary>
/// "Make your USB stick": find the stick, then download, check, write and check again. Only USB
/// sticks are listed; erasing needs an explicit tick naming the stick; Cancel works throughout.
/// </summary>
public sealed partial class UsbViewModel : ObservableObject
{
    private readonly JourneyViewModel _owner;
    private readonly UsbPlan _plan;
    private CancellationTokenSource? _cts;

    public UsbViewModel(UsbPlan plan, JourneyViewModel owner)
    {
        _plan = plan;
        _owner = owner;
    }

    public string Verification => _plan.Verification;
    public long ImageBytes => _plan.InstallerBytes ?? 8_000_000_000;

    /// <summary>waiting | choose | working | done | stopped | failed</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(IsWorking), nameof(CanStart))]
    public partial string State { get; private set; } = "waiting";

    [ObservableProperty] public partial IReadOnlyList<StickViewModel> Sticks { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EraseText), nameof(CanStart))]
    public partial StickViewModel? Selected { get; set; }

    partial void OnSelectedChanged(StickViewModel? oldValue, StickViewModel? newValue)
    {
        // A different stick needs a fresh "erase" tick.
        if (oldValue?.Disk != newValue?.Disk) Confirmed = false;
        foreach (var s in Sticks) s.IsSelected = s == newValue;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial bool Confirmed { get; set; }

    public string? EraseText => Selected is null ? null : $"I understand everything on the {Selected.Name} stick will be erased.";
    public bool CanStart => State == "choose" && Selected is { Check.CanUse: true } && Confirmed;
    public bool IsDone => State == "done";
    public bool IsWorking => State == "working";

    [ObservableProperty] public partial string? Stage { get; private set; }
    [ObservableProperty] public partial double Progress { get; private set; }
    [ObservableProperty] public partial string? Message { get; private set; }
    /// <summary>Shown just before Windows' permission prompt.</summary>
    [ObservableProperty] public partial string? PermissionNote { get; private set; }

    /// <summary>
    /// Looks for USB sticks. The view calls this every couple of seconds while the screen is
    /// showing. Nothing changes (and the erase tick stays) unless the sticks plugged in change.
    /// </summary>
    public async Task RefreshSticksAsync()
    {
        if (State is "working" or "done") return;
        var disks = (await _owner.Switch.ListUsbSticksAsync(CancellationToken.None)).Where(UsbSafety.IsListed).ToList();
        if (disks.SequenceEqual(Sticks.Select(s => s.Disk)) && State != "waiting") return;

        var previous = Selected?.Disk;
        Sticks = disks.Select(d => new StickViewModel(d, UsbSafety.Check(d, ImageBytes), s => Selected = s)).ToList();
        // Keep the choice if the same stick is still there; otherwise pick the only usable one.
        Selected = Sticks.FirstOrDefault(s => s.Disk == previous) ?? (Sticks.Count(s => s.Check.CanUse) == 1 ? Sticks.First(s => s.Check.CanUse) : null);
        State = Sticks.Count == 0 ? "waiting" : "choose";
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!CanStart || Selected is not { } stick) return;
        _cts = new CancellationTokenSource();
        State = "working";
        Message = null;
        try
        {
            var source = _owner.Switch.InstallerFor(_owner.DistroId, _owner.EditionId)
                         ?? throw new InstallerCheckException("GreyHaven doesn't know where to download this version of Linux.");
            Stage = "Finding the official download";
            var installer = await _owner.Switch.ResolveInstallerAsync(source, _cts.Token);
            var path = await _owner.Switch.DownloadAsync(installer, new Progress<MediaProgress>(Report), _cts.Token);
            PermissionNote = HelperReasons.Why(HelperAction.WriteUsb);
            var result = await _owner.Switch.RunElevatedAsync(HelperAction.WriteUsb,
                r => r with { Disk = stick.Disk.Number, Model = stick.Disk.Model, Size = stick.Disk.SizeBytes, Image = path, Sha256 = installer.Sha256 },
                new Progress<HelperMessage>(m => Report(new MediaProgress(m.Stage ?? "", m.Done, m.Total))), _cts.Token);
            PermissionNote = null;
            Message = result.Text;
            State = result.Kind == HelperMessage.Ok ? "done" : result.Kind == HelperMessage.Refused && _cts.IsCancellationRequested ? "stopped" : "failed";
        }
        catch (OperationCanceledException)
        {
            State = "stopped";
            Message = "Stopped. Nothing on your PC changed. The USB stick isn't ready; make it again when you like.";
        }
        catch (Exception ex) when (ex is InstallerCheckException or HttpRequestException or IOException)
        {
            State = "failed";
            Message = ex is HttpRequestException ? "The download didn't work. Check your internet connection and try again." : ex.Message;
        }
        finally
        {
            PermissionNote = null;
            _owner.Refresh();
        }
    }

    private void Report(MediaProgress p)
    {
        Stage = p.Stage;
        Progress = p.Fraction ?? 0;
    }

    [RelayCommand] private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private async Task TryAgainAsync()
    {
        State = "waiting";
        Confirmed = false;
        await RefreshSticksAsync();
    }
}

/// <summary>
/// The white-glove walkthrough (Parts 1–4 in the app, then a look at the guide for Part 5),
/// built from <see cref="JourneyBuilder"/>: the same steps the saved report and the guides use.
/// </summary>
public sealed partial class JourneyViewModel : ObservableObject
{
    private readonly Action _backToResults;
    private readonly Action<CompatibilityReport> _reportChanged;
    private IReadOnlyList<JourneyStep> _steps = [];
    private readonly Dictionary<string, object> _state = new();

    public JourneyViewModel(CompatibilityReport report, IAppServices services, ISwitchServices switchServices, Action backToResults, Action<CompatibilityReport>? reportChanged = null)
    {
        Services = services;
        Switch = switchServices;
        _backToResults = backToResults;
        _reportChanged = reportChanged ?? (_ => { });
        Report = report.SelectedInstall is null && JourneyBuilder.Suggested(report) is { } suggested ? report.WithInstall(suggested) : report;
        Rebuild();
        Go(0);
    }

    public IAppServices Services { get; }
    public ISwitchServices Switch { get; }
    public CompatibilityReport Report { get; private set; }
    public string DistroId => (Report.SelectedRecommendation ?? Report.Recommendation.Paths[0]).DistroId;
    public string EditionId => (Report.SelectedRecommendation ?? Report.Recommendation.Paths[0]).EditionId;

    /// <summary>App screens: Parts 1–4, then one "After you restart" screen.</summary>
    public int Count => _steps.Count(s => s.Phase != JourneyPhase.OnTheStick) + 1;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(IsLast))] public partial int Index { get; private set; }
    [ObservableProperty] public partial JourneyScreen Screen { get; private set; }
    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string Lead { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<Block> Body { get; private set; } = [];
    [ObservableProperty] public partial string? Tip { get; private set; }
    [ObservableProperty] public partial string Announcement { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<PhaseViewModel> Phases { get; private set; } = [];

    [ObservableProperty] public partial IReadOnlyList<InstallChoiceViewModel> Choices { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<CheckItemViewModel> Checklist { get; private set; } = [];
    [ObservableProperty] public partial bool ChecklistRequired { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<TaskViewModel> Tasks { get; private set; } = [];
    [ObservableProperty] public partial UsbViewModel? Usb { get; private set; }
    [ObservableProperty] public partial GuideViewModel? Guide { get; private set; }
    [ObservableProperty] public partial RestartViewModel? Restart { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<JourneyStep> Ahead { get; private set; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NextHint))] public partial bool CanGoOn { get; private set; }
    [ObservableProperty] public partial string NextLabel { get; private set; } = "";

    public bool CanGoBack => Index > 0;
    public bool IsLast => Index == Count - 1;
    public string? NextHint => CanGoOn ? null : Screen switch
    {
        JourneyScreen.Choose => "Choose how to install to go on.",
        JourneyScreen.Checklist => "Tick every item to go on.",
        JourneyScreen.Usb => "Make your USB stick to go on.",
        _ => null,
    };

    private void Rebuild() => _steps = JourneyBuilder.Build(Report);

    private IReadOnlyList<JourneyStep> AppSteps => _steps.Where(s => s.Phase != JourneyPhase.OnTheStick).ToList();

    public void Choose(InstallChoice choice)
    {
        if (Report.SelectedInstall == choice) return;
        Report = Report.WithInstall(choice);
        _reportChanged(Report);
        _state.Clear(); // backup and task ticks belong to the old choice
        Rebuild();
        foreach (var c in Choices) c.IsSelected = c.Choice == choice;
        Refresh();
    }

    public void Go(int index)
    {
        Index = Math.Clamp(index, 0, Count - 1);
        var steps = AppSteps;
        if (Index < steps.Count) Show(steps[Index]);
        else ShowAfterRestart();
        var next = Index + 1 < steps.Count ? steps[Index + 1].Title : Index + 1 == steps.Count ? "After you restart" : null;
        NextLabel = next is null ? "Done" : $"Next: {next} →";
        var phase = Index < steps.Count ? steps[Index].Phase : JourneyPhase.OnTheStick;
        Phases = Enum.GetValues<JourneyPhase>().Select(p => new PhaseViewModel((int)p, JourneyBuilder.PhaseName(p), p < phase ? "done" : p == phase ? "now" : "next")).ToList();
        Announcement = $"{JourneyBuilder.PhaseName(phase)}: {Title}";
        Refresh();
    }

    private void Show(JourneyStep s)
    {
        Title = s.Title;
        Lead = s.Lead;
        Tip = s.Tip;
        Choices = [];
        Checklist = [];
        ChecklistRequired = false;
        Tasks = [];
        Usb = null;
        Guide = null;
        Restart = null;
        Ahead = [];
        var body = s.Body.ToList();

        if (s.Choices.Count > 0)
        {
            Screen = JourneyScreen.Choose;
            Choices = s.Choices.Select(c => new InstallChoiceViewModel(c, this)).ToList();
            foreach (var c in Choices) c.IsSelected = c.Choice == Report.SelectedInstall && c.Possible;
        }
        else if (s.Id == "backup" && body.OfType<ListBlock>().FirstOrDefault(l => l.Kind == ListKind.Checkboxes) is { } list)
        {
            Screen = JourneyScreen.Checklist;
            ChecklistRequired = s.ChecklistRequired;
            Checklist = Remember($"backup-{Report.SelectedInstall}", () => list.Items.Select(i => new CheckItemViewModel(string.Concat(i.Select(Plain)), Refresh)).ToList());
            body.Remove(list);
        }
        else if (s.Tasks.Count > 0)
        {
            Screen = JourneyScreen.Tasks;
            Tasks = Remember($"tasks-{s.Id}-{Report.SelectedInstall}", () => s.Tasks.Select(t => new TaskViewModel(t, this)).ToList());
        }
        else if (s.Usb is { } usb)
        {
            Screen = JourneyScreen.Usb;
            Usb = Remember($"usb-{DistroId}-{EditionId}", () => new UsbViewModel(usb, this));
        }
        else if (s.Guide is { } guide)
        {
            Screen = JourneyScreen.Guide;
            Guide = new GuideViewModel(Report, guide, this);
        }
        else if (s.Restart is { } restart)
        {
            Screen = JourneyScreen.Restart;
            Restart = Remember("restart", () => new RestartViewModel(restart, this));
        }
        else Screen = JourneyScreen.Blocks;
        Body = body;
    }

    private void ShowAfterRestart()
    {
        Screen = JourneyScreen.AfterRestart;
        Title = "After you restart";
        Lead = "From here, you follow your guide on your phone or on paper.";
        Tip = null;
        Body = [];
        Choices = []; Checklist = []; Tasks = []; Usb = null; Guide = null; Restart = null;
        Ahead = JourneyBuilder.OnTheStick(Report);
    }

    private T Remember<T>(string key, Func<T> make) where T : class
    {
        if (_state.TryGetValue(key, out var v) && v is T t) return t;
        var made = make();
        _state[key] = made;
        return made;
    }

    private static string Plain(Inline i) => i switch { Text t => t.Value, Strong s => s.Value, Link l => l.Label, Code c => c.Value, Fill f => f.General, _ => "" };

    public void Refresh()
    {
        CanGoOn = Screen switch
        {
            JourneyScreen.Choose => Choices.Any(c => c.IsSelected && c.Possible),
            JourneyScreen.Checklist => !ChecklistRequired || Checklist.All(c => c.IsChecked),
            JourneyScreen.Usb => Usb is { IsDone: true } || Switch.IsDemo && Usb is { IsWorking: false },
            _ => true,
        };
    }

    [RelayCommand]
    private void Next()
    {
        if (!CanGoOn) return;
        if (IsLast) _backToResults();
        else Go(Index + 1);
    }

    [RelayCommand] private void Back() => Go(Index - 1);
    [RelayCommand] private void BackToResults() => _backToResults();

    public sealed record PhaseViewModel(int Number, string Name, string State)
    {
        public string AccessibleName => $"Part {Number}: {Name}, {(State == "done" ? "done" : State == "now" ? "you are here" : "still to come")}";
    }
}

/// <summary>"Take your guide with you": the QR code to the hosted phone guide, print, and the copy in Documents.</summary>
public sealed partial class GuideViewModel : ObservableObject
{
    private readonly JourneyViewModel _owner;
    private readonly CompatibilityReport _report;

    public GuideViewModel(CompatibilityReport report, GuideHandoff guide, JourneyViewModel owner)
    {
        _report = report;
        _owner = owner;
        PhoneUrl = guide.PhoneUrl;
        QrModules = guide.PhoneUrl is { } url ? QrCode.Modules(url) : null;
        // The person's own copy (with their app list) goes in Documents, readable from Linux too.
        SavedPath = guide.OnWindowsDrive is null ? null : owner.Switch.SaveGuide(HtmlReportWriter.Write(PhoneGuideBuilder.Build(report)));
    }

    public string? PhoneUrl { get; }
    public bool[][]? QrModules { get; }
    public string? SavedPath { get; }
    public string? SavedText => SavedPath is null ? null : $"Saved on your Windows drive: {SavedPath}";

    /// <summary>Opens the guide in the browser, where it can be printed (Ctrl+P) or saved.</summary>
    [RelayCommand]
    private void OpenGuide()
    {
        if (SavedPath is not null) _owner.Services.Open(SavedPath);
        else if (PhoneUrl is not null) _owner.Services.Open(PhoneUrl);
    }
}

/// <summary>"Restart into your USB stick": final checks, then one button.</summary>
public sealed partial class RestartViewModel : ObservableObject
{
    private readonly JourneyViewModel _owner;

    public RestartViewModel(RestartPlan plan, JourneyViewModel owner)
    {
        _owner = owner;
        Plan = plan;
        Checks = plan.FinalChecks.Select(c => new CheckItemViewModel(c, () => OnPropertyChanged(nameof(CanRestart)))).ToList();
    }

    public RestartPlan Plan { get; }
    public IReadOnlyList<CheckItemViewModel> Checks { get; }
    public bool CanRestart => Checks.All(c => c.IsChecked);
    [ObservableProperty] public partial string? Message { get; private set; }

    [RelayCommand]
    private void RestartNow()
    {
        if (!CanRestart) return;
        if (_owner.Switch.IsDemo) { Message = "Demo: your PC would restart now, to Windows' blue \"Choose an option\" screen."; return; }
        Message = _owner.Switch.RestartToAdvancedStartup()
            ? "Restarting…"
            : "Windows didn't restart. Follow \"If GreyHaven can't restart your PC\" below to do it from Settings.";
    }
}
