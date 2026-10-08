using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using GreyHaven.Media;
using GreyHaven.Scan.Windows;
using Microsoft.Win32;

namespace GreyHaven.Helper;

/// <summary>
/// Does one thing with administrator rights, then exits. It never trusts the app's word about
/// the disk: it lists disks itself and refuses unless the disk is still the same USB stick and
/// passes every rule in <see cref="UsbSafety"/>. Progress goes back over the app's named pipe.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 2;
        HelperRequest request;
        try { request = HelperRequest.Parse(args); }
        catch (ArgumentException) { return 2; }

        await using var pipe = new NamedPipeClientStream(".", request.Pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        try { await pipe.ConnectAsync(10_000); }
        catch (TimeoutException) { return 2; }
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(request.Token);

        using var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { while (await reader.ReadLineAsync() is { } line) if (line == "cancel") await cts.CancelAsync(); }
            catch (IOException) { await cts.CancelAsync(); } // the app went away: stop safely
        });
        var send = (HelperMessage m) => { lock (writer) writer.WriteLine(m.ToLine()); };

        try
        {
            var done = request.Action switch
            {
                HelperAction.FastStartupOff => SetFastStartup(false),
                HelperAction.FastStartupOn => SetFastStartup(true),
                HelperAction.BitLockerOff => BitLockerOff(),
                _ => await WriteUsbAsync(request, m => send(m), cts.Token),
            };
            send(done);
            return done.Kind == HelperMessage.Ok ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            send(new HelperMessage(HelperMessage.Error, Text: "Stopped. Nothing on your PC changed. The USB stick isn't ready; make it again when you like."));
            return 3;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Management.ManagementException)
        {
            send(new HelperMessage(HelperMessage.Error, Text: ex.Message));
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private static HelperMessage SetFastStartup(bool on)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", writable: true)
                        ?? throw new InvalidOperationException("Windows' power settings weren't found.");
        key.SetValue("HiberbootEnabled", on ? 1 : 0, RegistryValueKind.DWord);
        return new HelperMessage(HelperMessage.Ok, Text: on ? "Fast Startup is on again." : "Fast Startup is off.");
    }

    [SupportedOSPlatform("windows")]
    private static HelperMessage BitLockerOff()
    {
        // Only ever the Windows drive, never a drive named by the caller.
        var drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        if (drive.Length != 2 || drive[1] != ':' || !char.IsAsciiLetter(drive[0])) throw new InvalidOperationException("The Windows drive couldn't be found.");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "manage-bde.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-off");
        start.ArgumentList.Add(drive);
        using var p = Process.Start(start) ?? throw new InvalidOperationException("Windows' BitLocker tool didn't start.");
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0
            ? new HelperMessage(HelperMessage.Ok, Text: "Turning off encryption has started. It carries on in the background and can take an hour or more. Your files stay the same.")
            : new HelperMessage(HelperMessage.Error, Text: "Windows couldn't turn off encryption. Try it in Control Panel > BitLocker Drive Encryption.");
    }

    [SupportedOSPlatform("windows")]
    private static async Task<HelperMessage> WriteUsbAsync(HelperRequest r, Action<HelperMessage> send, CancellationToken ct)
    {
        // 1. The disk must still be the stick the person chose, and pass every rule.
        var disk = WindowsDisks.List().FirstOrDefault(d => d.Info.Number == r.Disk)
                   ?? throw new InvalidOperationException("The USB stick isn't plugged in any more.");
        if (disk.Info.Model != r.Model || disk.Info.SizeBytes != r.Size)
            return new HelperMessage(HelperMessage.Refused, Text: "The USB stick changed since you chose it. Choose it again in GreyHaven.");
        var image = new FileInfo(r.Image!);
        if (!image.Exists) throw new InvalidOperationException("The Linux installer file is missing. Download it again.");
        var check = UsbSafety.Check(disk.Info, image.Length);
        if (!check.CanUse) return new HelperMessage(HelperMessage.Refused, Text: check.Problem);

        // 2. The file must still be the checked one.
        await using (var f = new FileStream(image.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
        {
            var sha = await Checksums.Sha256Async(f, null, new Progress<long>(d => send(new HelperMessage(HelperMessage.Progress, InstallerDownloader.Checking, d, image.Length))), ct);
            if (sha != r.Sha256) return new HelperMessage(HelperMessage.Refused, Text: "The installer file changed after it was checked. Download it again.");
        }

        // 3. Lock and dismount the stick's volumes, write, read back, and let Windows see the new layout.
        using var volumes = RawDisk.LockVolumes(disk.Info.Number);
        await using var raw = RawDisk.Open(disk.Info.Number, disk.Info.SizeBytes, disk.SectorSize);
        await using var source = new FileStream(image.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        ImageWriter.ClearEnd(raw, disk.Info.SizeBytes, disk.SectorSize);
        var progress = new Progress<MediaProgress>(p => send(new HelperMessage(HelperMessage.Progress, p.Stage, p.Done, p.Total)));
        await ImageWriter.WriteAndVerifyAsync(source, image.Length, raw, disk.SectorSize, r.Sha256!, progress, ct);
        raw.RefreshLayout();
        return new HelperMessage(HelperMessage.Ok, Text: "Your USB stick is ready.");
    }
}
