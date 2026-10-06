using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>Builds the live-USB test guide for each distro, for this PC (data/live-usb.yaml).</summary>
public static class LiveUsbGuides
{
    public static IReadOnlyList<LiveUsbGuide> Build(
        HardwareProfile profile,
        IReadOnlyList<RatedComponent> rated,
        IReadOnlyList<DistroSummary> summaries,
        IReadOnlyList<Caveat> caveats,
        CompatibilityData data)
    {
        var categories = profile.Components.Select(c => c.Category).ToHashSet();
        var checks = data.LiveUsb.Checks
            .Where(c => categories.Any(c.AppliesTo))
            .Select(c => new LiveUsbCheck(c.Title, c.Test))
            .Concat(caveats.Select(c => new LiveUsbCheck(c.Title, c.WhatToTest)))
            .ToList();
        var probe = data.LiveUsb.HwProbe;

        return summaries.Select(summary =>
        {
            var info = data.Distros.Distros.First(d => d.Id == summary.DistroId);
            return new LiveUsbGuide
            {
                DistroId = info.Id,
                DistroName = $"{info.Name} {info.Version}",
                EditionName = summary.Edition,
                Website = info.Website,
                MakeUsbGuide = ToSource(info.LiveUsb.MakeUsb),
                TryItGuide = ToSource(info.LiveUsb.TryIt),
                InstallerBytes = info.Editions.FirstOrDefault(e => e.Id == summary.EditionId)?.InstallerBytes,
                Checks = checks,
                BeforeYouStart = BeforeYouStart(profile.System, info.Name, info.SecureBoot.Supported),
                ExpectedInLiveSession = rated
                    .Where(r => r.For(info.Id).Rating == Rating.WorksWithExtraDriver)
                    .Select(r => $"{Labels.Of(r.Component.Category)} ({r.DisplayName}) needs an extra step after installing, so it may not work fully in the live session. That's expected.")
                    .ToList(),
                ShareCommands = probe.CommandsFor(info.Id),
                ShareNote = probe.Note,
                ShareSource = ToSource(probe.Source),
            };
        }).ToList();
    }

    private static List<string> BeforeYouStart(SystemInfo system, string distroName, bool secureBootSupported)
    {
        var steps = new List<string>();
        if (system.IsArm)
            steps.Add($"This PC has an ARM processor. Download {distroName}'s ARM64 version if there is one; the usual download is for Intel and AMD PCs and won't start.");
        if (!secureBootSupported && system.SecureBoot is SecureBootState.Enabled or SecureBootState.Unknown)
            steps.Add($"{distroName} won't start while Secure Boot is on, so turn Secure Boot off first. Follow the how-to guide in the recommendation.");
        if (system.BitLocker is not (BitLockerState.NotEncrypted or BitLockerState.Unknown))
            steps.Add("Your drive is encrypted with BitLocker. Changing firmware settings can make Windows ask for the BitLocker recovery key when it next starts, so write the key down first. You can find it at https://aka.ms/myrecoverykey (sign in with your Microsoft account).");
        if (system.StorageMode is StorageControllerMode.IntelVmd or StorageControllerMode.IntelRstRaid or StorageControllerMode.Raid)
            steps.Add("Your storage controller is in a mode Linux often can't use, so the live session may not show your internal drive. That's expected and doesn't affect these tests.");
        return steps;
    }

    private static Source? ToSource(Compat.Data.SourceLink? s) =>
        s is null ? null : new Source(s.Title, s.Url, SourceKind.Reference);
}
