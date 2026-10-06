using System.Text;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Cli;

/// <summary>Readable terminal summary of a <see cref="HardwareProfile"/>.</summary>
internal static class ProfileTextFormatter
{
    public static string Format(HardwareProfile p)
    {
        var sb = new StringBuilder();
        var s = p.System;

        sb.AppendLine($"GreyHaven scan  ({p.Source}, {p.ScannedAt:yyyy-MM-dd HH:mm} UTC)");
        if (p.Description is not null) sb.AppendLine(p.Description);
        sb.AppendLine();

        sb.AppendLine("SYSTEM");
        Row(sb, "PC", Join(s.SystemManufacturer, s.SystemModel) ?? "Unknown");
        if (s.BoardManufacturer is not null) Row(sb, "Motherboard", s.BoardManufacturer);
        Row(sb, "Type", Labels.Of(s.FormFactor));
        Row(sb, "Windows", s.Windows.FriendlyName);
        Row(sb, "Architecture", Labels.Of(s.Architecture));
        Row(sb, "Boot mode", Labels.Of(s.Firmware));
        Row(sb, "Secure Boot", Labels.Of(s.SecureBoot));
        Row(sb, "Storage mode", Labels.Of(s.StorageMode));
        Row(sb, "Fast Startup", Labels.Of(s.FastStartup));
        Row(sb, "BitLocker", Labels.Of(s.BitLocker));
        sb.AppendLine();

        sb.AppendLine($"COMPONENTS ({p.Components.Count})");
        foreach (var group in p.Components.GroupBy(c => c.Category))
        {
            sb.AppendLine($"  {Labels.Of(group.Key)}");
            foreach (var c in group)
            {
                var count = c.Count > 1 ? $" (x{c.Count})" : "";
                sb.AppendLine($"    {c.Name}{count}");
                var details = new List<string>();
                if (c.Id is not null) details.Add($"{c.Id.Bus.ToString().ToUpperInvariant()} {c.Id.VendorDevice}");
                if (c.Id?.HasSubsystem == true) details.Add($"subsystem {c.Id.SubVendor}:{c.Id.SubDevice}");
                if (c.Id?.ClassCode is not null) details.Add($"class {c.Id.ClassCode}");
                if (c.Cpu is { } cpu) details.Add($"{cpu.Vendor} family {cpu.Family} model {cpu.Model}, {cpu.Cores} cores");
                if (c.WindowsDriver is not null) details.Add($"Windows driver {c.WindowsDriver}");
                if (c.WindowsProblemCode is > 0) details.Add(c.WindowsProblemCode == 28 ? "NO WINDOWS DRIVER" : $"problem code {c.WindowsProblemCode}");
                sb.AppendLine($"      {string.Join(" | ", details)}");
            }
        }

        if (p.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("NOTES");
            foreach (var w in p.Warnings) sb.AppendLine($"  ! {w}");
        }
        return sb.ToString().TrimEnd();
    }

    private static void Row(StringBuilder sb, string label, string value) =>
        sb.AppendLine($"  {label,-14}{value}");

    private static string? Join(params string?[] parts)
    {
        var joined = string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        return joined.Length == 0 ? null : joined;
    }
}
