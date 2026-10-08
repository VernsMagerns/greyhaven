using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;
using GreyHaven.Media;
using GreyHaven.Presentation;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.Tests.Presentation;

/// <summary>A switch-services fake that records what it was asked to do.</summary>
internal sealed class FakeSwitch : ISwitchServices
{
    public bool IsDemo { get; init; }
    public List<DiskInfo> Sticks { get; } = [new DiskInfo(4, "SanDisk Ultra", 16_008_609_792, true, true, false, false, false)];
    public List<(HelperAction Action, HelperRequest? Request)> Elevated { get; } = [];
    public HelperMessage HelperResult { get; set; } = new(HelperMessage.Ok, Text: "Your USB stick is ready.");
    public Func<CancellationToken, Task>? DuringDownload { get; set; }
    public int Restarts { get; private set; }
    public string? Saved { get; private set; }

    public Task<IReadOnlyList<DiskInfo>> ListUsbSticksAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DiskInfo>>(Sticks.ToList());
    public InstallerSource? InstallerFor(string distroId, string editionId) => new() { DistroName = distroId, Api = "https://api.pop-os.org/x" };
    public Task<ResolvedInstaller> ResolveInstallerAsync(InstallerSource source, CancellationToken ct) =>
        Task.FromResult(new ResolvedInstaller("https://x/x.iso", "x.iso", new string('b', 64), 6_000_000_000, CheckKind.SignedChecksum));
    public async Task<string> DownloadAsync(ResolvedInstaller installer, IProgress<MediaProgress> progress, CancellationToken ct)
    {
        if (DuringDownload is not null) await DuringDownload(ct);
        ct.ThrowIfCancellationRequested();
        return @"C:\Users\x\AppData\Local\GreyHaven\downloads\x.iso";
    }
    public Task<HelperMessage> RunElevatedAsync(HelperAction action, Func<HelperRequest, HelperRequest>? details, IProgress<HelperMessage>? progress, CancellationToken ct)
    {
        var request = details?.Invoke(new HelperRequest { Action = action, Pipe = "p", Token = "t" });
        Elevated.Add((action, request));
        return Task.FromResult(HelperResult);
    }
    public bool RestartToAdvancedStartup() { Restarts++; return true; }
    public string? SaveGuide(string html) { Saved = html; return @"C:\Users\x\Documents\GreyHaven\GreyHaven guide.html"; }
}

public class JourneyViewModelTests
{
    private static async Task<(JourneyViewModel Vm, FakeSwitch Switch)> Start(string sample = "ryzen-rtx2070s-desktop", Func<HardwareProfile, HardwareProfile>? change = null, Func<CompatibilityReport, CompatibilityReport>? report = null)
    {
        var r = await FakeServices.RealReport(sample, change);
        if (report is not null) r = report(r);
        var sw = new FakeSwitch();
        return (new JourneyViewModel(r, FakeServices.ForSample(sample), sw, () => { }), sw);
    }

    private static void GoTo(JourneyViewModel vm, JourneyScreen screen)
    {
        for (var i = 0; i < vm.Count && vm.Screen != screen; i++) vm.Go(i);
        Assert.Equal(screen, vm.Screen);
    }

    [Fact]
    public async Task Starts_with_the_install_choice_and_keep_windows_suggested_and_picked()
    {
        var (vm, _) = await Start();
        Assert.Equal(JourneyScreen.Choose, vm.Screen);
        Assert.Equal("How do you want to install Linux?", vm.Title);
        var keep = vm.Choices.Single(c => c.Choice == InstallChoice.KeepWindows);
        Assert.True(keep.Recommended && keep.IsSelected);
        Assert.True(vm.CanGoOn);
        Assert.Equal("now", vm.Phases[0].State);
        Assert.Equal(["Get ready", "Make your USB stick", "Take your guide", "Restart", "On the USB stick"], vm.Phases.Select(p => p.Name));
    }

    [Fact]
    public async Task Replacing_windows_requires_every_backup_item()
    {
        var (vm, _) = await Start();
        vm.Choices.Single(c => c.Choice == InstallChoice.ReplaceWindows).IsSelected = true;
        Assert.Equal(InstallChoice.ReplaceWindows, vm.Report.SelectedInstall);
        vm.Go(1);
        Assert.Equal(JourneyScreen.Checklist, vm.Screen);
        Assert.True(vm.ChecklistRequired);
        Assert.False(vm.CanGoOn);
        Assert.Equal("Tick every item to go on.", vm.NextHint);
        foreach (var c in vm.Checklist) c.IsChecked = true;
        Assert.True(vm.CanGoOn);
    }

    [Fact]
    public async Task One_click_task_asks_first_explains_the_permission_prompt_and_can_be_undone()
    {
        var (vm, sw) = await Start(change: p => p with { System = p.System with { FastStartup = FastStartupState.Enabled } });
        vm.Go(2);
        Assert.Equal(JourneyScreen.Tasks, vm.Screen);
        var task = vm.Tasks.Single(t => t.Title == "Turn off Fast Startup");
        Assert.True(task.ShowButton);
        task.AskCommand.Execute(null);
        Assert.True(task.ShowConfirm);
        Assert.Empty(sw.Elevated);                                                // nothing yet: it asks first
        Assert.Equal("Windows will ask for permission so GreyHaven can change the Fast Startup setting.", task.WhyPermission);
        await task.DoItCommand.ExecuteAsync(null);
        Assert.Equal(HelperAction.FastStartupOff, sw.Elevated.Single().Action);
        Assert.True(task.IsDone && task.CanUndo);
        await task.UndoCommand.ExecuteAsync(null);
        Assert.Equal(HelperAction.FastStartupOn, sw.Elevated[1].Action);
    }

    [Fact]
    public async Task Usb_stick_is_written_only_after_choosing_and_confirming_the_erase()
    {
        var (vm, sw) = await Start();
        GoTo(vm, JourneyScreen.Usb);
        var usb = vm.Usb!;
        Assert.False(vm.CanGoOn);
        await usb.RefreshSticksAsync();
        Assert.Equal("SanDisk Ultra, 16 GB", usb.Selected!.Name);
        Assert.False(usb.CanStart);                                               // not confirmed yet
        Assert.Equal("I understand everything on the SanDisk Ultra, 16 GB stick will be erased.", usb.EraseText);
        usb.Confirmed = true;
        await usb.RefreshSticksAsync();                                           // the regular re-check keeps the tick
        Assert.True(usb.CanStart);
        sw.Sticks.Add(new DiskInfo(6, "Other stick", 32_000_000_000, true, true, false, false, false));
        await usb.RefreshSticksAsync();                                           // a stick just plugged in is suggested, with a fresh tick
        Assert.Equal("Other stick, 32 GB", usb.Selected!.Name);
        Assert.False(usb.CanStart);
        sw.Sticks.RemoveAt(1);
        await usb.RefreshSticksAsync();                                           // it's gone: back to the first one
        Assert.Equal("SanDisk Ultra, 16 GB", usb.Selected!.Name);
        usb.Confirmed = true;
        await usb.StartCommand.ExecuteAsync(null);
        var (action, request) = sw.Elevated.Single();
        Assert.Equal(HelperAction.WriteUsb, action);
        Assert.Equal((4, "SanDisk Ultra", 16_008_609_792L, new string('b', 64)), (request!.Disk!.Value, request.Model, request.Size!.Value, request.Sha256));
        Assert.True(usb.IsDone);
        Assert.True(vm.CanGoOn);
    }

    [Fact]
    public async Task With_several_sticks_in_at_the_start_the_newest_is_suggested_and_contents_are_shown()
    {
        var (vm, sw) = await Start();
        sw.Sticks.Clear();
        sw.Sticks.Add(new DiskInfo(7, "Kingston", 32_000_000_000, true, true, false, false, false, "F:", 3_240_000_000));
        sw.Sticks.Add(new DiskInfo(3, "SanDisk", 16_000_000_000, true, true, false, false, false, "E:", 0));
        GoTo(vm, JourneyScreen.Usb);
        await vm.Usb!.RefreshSticksAsync();
        Assert.Equal("Kingston, 32 GB", vm.Usb.Selected!.Name);
        Assert.Equal(["F: 3.2 GB used", "E: empty"], vm.Usb.Sticks.Select(s => s.Contents));
        sw.Sticks[0] = sw.Sticks[0] with { Drives = null, UsedBytes = null };
        await vm.Usb.RefreshSticksAsync();
        Assert.Equal("Windows can't show what's on it.", vm.Usb.Sticks[0].Contents);
        Assert.Equal("Kingston, 32 GB", vm.Usb.Selected!.Name);
    }

    [Fact]
    public async Task Too_small_sticks_cant_be_used_and_internal_drives_are_never_listed()
    {
        var (vm, sw) = await Start();
        sw.Sticks.Clear();
        sw.Sticks.Add(new DiskInfo(5, "Tiny", 4_000_000_000, true, true, false, false, false));
        sw.Sticks.Add(new DiskInfo(0, "Samsung SSD", 1_000_000_000_000, false, false, true, false, false));
        GoTo(vm, JourneyScreen.Usb);
        await vm.Usb!.RefreshSticksAsync();
        var stick = Assert.Single(vm.Usb.Sticks);
        Assert.Contains("too small", stick.Problem);
        vm.Usb.Selected = stick;
        vm.Usb.Confirmed = true;
        Assert.False(vm.Usb.CanStart);
    }

    [Fact]
    public async Task Cancelling_stops_safely()
    {
        var (vm, sw) = await Start();
        var gate = new TaskCompletionSource();
        sw.DuringDownload = async ct => { gate.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        GoTo(vm, JourneyScreen.Usb);
        await vm.Usb!.RefreshSticksAsync();
        vm.Usb.Confirmed = true;
        var run = vm.Usb.StartCommand.ExecuteAsync(null);
        await gate.Task;
        Assert.True(vm.Usb.IsWorking);
        vm.Usb.CancelCommand.Execute(null);
        await run;
        Assert.Equal("stopped", vm.Usb.State);
        Assert.StartsWith("Stopped. Nothing on your PC changed.", vm.Usb.Message);
        Assert.Empty(sw.Elevated);
    }

    [Fact]
    public async Task Guide_has_the_hosted_qr_code_and_a_copy_with_the_apps_in_documents()
    {
        var (vm, sw) = await Start("nvidia-desktop");
        GoTo(vm, JourneyScreen.Guide);
        Assert.StartsWith("https://greyhaven-guide.pages.dev/ubuntu-desktop-keep#", vm.Guide!.PhoneUrl);
        Assert.NotNull(vm.Guide.QrModules);
        Assert.Contains("OpenWhispr", sw.Saved);                                  // the private copy has the app list
        Assert.StartsWith("Saved on your Windows drive:", vm.Guide.SavedText);
    }

    [Fact]
    public async Task Restart_needs_every_final_check()
    {
        var (vm, sw) = await Start();
        GoTo(vm, JourneyScreen.Restart);
        var restart = vm.Restart!;
        restart.RestartNowCommand.Execute(null);
        Assert.Equal(0, sw.Restarts);
        foreach (var c in restart.Checks) c.IsChecked = true;
        Assert.True(restart.CanRestart);
        restart.RestartNowCommand.Execute(null);
        Assert.Equal(1, sw.Restarts);
    }

    [Fact]
    public async Task Last_screen_previews_the_guide_steps()
    {
        var (vm, _) = await Start();
        vm.Go(vm.Count - 1);
        Assert.Equal(JourneyScreen.AfterRestart, vm.Screen);
        Assert.Equal(["Try Linux", "Install Ubuntu", "After installing"], vm.Ahead.Select(s => s.Title));
        Assert.Equal("Done", vm.NextLabel);
    }

    [Fact]
    public async Task Must_have_apps_shape_the_install_choice()
    {
        var (vm, _) = await Start("nvidia-desktop", report: r => r with { MustHaveApps = ["VALORANT"] });
        Assert.Contains(vm.Choices.Single(c => c.Choice == InstallChoice.KeepWindows).Checks, c => c.Text == "You can still use VALORANT on Windows.");
        Assert.Contains(vm.Choices.Single(c => c.Choice == InstallChoice.ReplaceWindows).Checks, c => c.Text.StartsWith("VALORANT won't work on Linux"));
    }

    [Fact]
    public async Task Demo_mode_never_restarts_or_writes()
    {
        var r = await FakeServices.RealReport("nvidia-desktop");
        var demo = new DemoSwitchServices(TimeSpan.Zero);
        var vm = new JourneyViewModel(r, FakeServices.ForSample("nvidia-desktop", demo: "nvidia-desktop"), demo, () => { });
        GoTo(vm, JourneyScreen.Restart);
        foreach (var c in vm.Restart!.Checks) c.IsChecked = true;
        vm.Restart.RestartNowCommand.Execute(null);
        Assert.StartsWith("Demo:", vm.Restart.Message);
        Assert.Equal("SanDisk Ultra (pretend)", (await demo.ListUsbSticksAsync(default)).Single().Model);
    }
}

public class AppsViewModelTests
{
    [Fact]
    public async Task Results_show_apps_and_must_have_warnings()
    {
        var services = FakeServices.ForSample("nvidia-desktop");
        var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
        Assert.True(vm.HasApps);
        Assert.Equal("9 apps work. 10 have a website or alternative. 4 apps won't work. We don't know 2 apps yet.", vm.AppsSummary);
        var valorant = vm.AppGroups.SelectMany(g => g.Apps).Single(a => a.Name == "VALORANT");
        Assert.Null(vm.MustHaveHeadline);
        valorant.IsMustHave = true;
        Assert.Contains("Keep Windows too", valorant.MustHaveWarning);
        Assert.StartsWith("1 app you need won't work on Linux.", vm.MustHaveHeadline);
        Assert.Equal(["VALORANT"], vm.Report.MustHaveApps);
        var obs = vm.AppGroups.SelectMany(g => g.Apps).Single(a => a.Name == "OBS Studio");
        Assert.Equal("Get it: App Center", obs.GetText);
        Assert.Equal(["Camera (HD Pro Webcam C920): Works right away"], obs.Hardware);
    }

    [Fact]
    public async Task Saving_leaves_apps_out_unless_ticked()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gh-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var services = FakeServices.ForSample("nvidia-desktop");
            var vm = new ResultsViewModel(await FakeServices.RealReport("nvidia-desktop"), services);
            services.SaveLocation = Path.Combine(dir, "a.md");
            await vm.SaveReportCommand.ExecuteAsync(null);
            Assert.DoesNotContain("## Your apps", await File.ReadAllTextAsync(services.SaveLocation));
            vm.IncludeApps = true;
            services.SaveLocation = Path.Combine(dir, "b.md");
            await vm.SaveReportCommand.ExecuteAsync(null);
            Assert.Contains("## Your apps", await File.ReadAllTextAsync(services.SaveLocation));
        }
        finally { Directory.Delete(dir, true); }
    }
}
