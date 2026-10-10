using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using GreyHaven.Media;

namespace GreyHaven.Scan.Windows;

/// <summary>
/// Starts GreyHaven.Helper.exe with Windows' permission prompt (UAC) for exactly one action, and
/// listens for its progress on a named pipe that only this user and administrators can open. The
/// helper must send the one-time token first; anything else is ignored. Cancelling sends "cancel"
/// to the helper.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHelperClient(string helperPath)
{
    private const int ErrorCancelled = 1223; // the person clicked "No" on the permission prompt

    /// <summary>
    /// Who may connect: this user, and the Administrators group. On a standard account, Windows'
    /// prompt asks for an administrator's password and the helper then runs as that administrator,
    /// a different user, so "this user only" would shut it out. Administrators already control the
    /// whole PC, so this lets no one new in; the one-time token still has to match.
    /// </summary>
    private static PipeSecurity PipeAccess()
    {
        var security = new PipeSecurity();
        var me = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Couldn't tell which Windows user is running GreyHaven.");
        security.AddAccessRule(new PipeAccessRule(me, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize, AccessControlType.Allow));
        return security;
    }

    public async Task<HelperMessage> RunAsync(HelperAction action, Func<HelperRequest, HelperRequest>? details, IProgress<HelperMessage>? progress, CancellationToken ct)
    {
        var request = new HelperRequest { Action = action, Pipe = HelperRequest.NewPipeName(), Token = HelperRequest.NewToken() };
        if (details is not null) request = details(request);

        await using var pipe = NamedPipeServerStreamAcl.Create(request.Pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 0, 0, PipeAccess());

        var start = new ProcessStartInfo(helperPath) { UseShellExecute = true, Verb = "runas" };
        foreach (var a in request.ToArgs()) start.ArgumentList.Add(a);
        Process? process;
        try { process = Process.Start(start); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new HelperMessage(HelperMessage.Refused, Text: "You chose not to give permission, so nothing was changed.");
        }
        if (process is null) return new HelperMessage(HelperMessage.Error, Text: "GreyHaven's helper didn't start.");

        using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connect.CancelAfter(TimeSpan.FromSeconds(60));
        try { await pipe.WaitForConnectionAsync(connect.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new HelperMessage(HelperMessage.Error, Text: "GreyHaven's helper didn't answer. Nothing was changed.");
        }

        using var reader = new StreamReader(pipe, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        if (await reader.ReadLineAsync(ct).ConfigureAwait(false) != request.Token)
            return new HelperMessage(HelperMessage.Error, Text: "Something other than GreyHaven's helper answered, so GreyHaven stopped.");

        await using var onCancel = ct.Register(() => { try { writer.WriteLine("cancel"); } catch (IOException) { } });
        HelperMessage? last = null;
        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            if (HelperMessage.FromLine(line) is not { } m) continue;
            if (m.Kind == HelperMessage.Progress) progress?.Report(m);
            else { last = m; break; }
        }
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        return last ?? new HelperMessage(HelperMessage.Error, Text: "GreyHaven's helper stopped without saying why.");
    }
}
