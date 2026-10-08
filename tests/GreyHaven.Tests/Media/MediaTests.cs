using System.Net;
using System.Security.Cryptography;
using GreyHaven.Media;

namespace GreyHaven.Tests.Media;

public class MediaTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "media", name);
    private static string Key(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "keys", name));
    private const string UbuntuKey = "843938DF228D22F7B3742BC0D94AA3F0EFE21092";
    private const string MintKey = "27DEB15644C6B3CF3BD7D291300F846BA25BAE09";

    // ---- Signatures and checksums (real files from releases.ubuntu.com and kernel.org, 2026-10-07) ----

    [Fact]
    public void Real_signed_checksum_lists_verify_with_the_pinned_keys()
    {
        Assert.True(Signatures.Verify(File.ReadAllBytes(Fixture("ubuntu-SHA256SUMS")), File.ReadAllBytes(Fixture("ubuntu-SHA256SUMS.gpg")), Key("ubuntu-cdimage.asc"), UbuntuKey));
        Assert.True(Signatures.Verify(File.ReadAllBytes(Fixture("mint-sha256sum.txt")), File.ReadAllBytes(Fixture("mint-sha256sum.txt.gpg")), Key("linuxmint-iso.asc"), MintKey));
    }

    [Fact]
    public void A_changed_list_a_wrong_key_or_an_unpinned_key_is_refused()
    {
        var list = File.ReadAllBytes(Fixture("ubuntu-SHA256SUMS"));
        var sig = File.ReadAllBytes(Fixture("ubuntu-SHA256SUMS.gpg"));
        var changed = (byte[])list.Clone();
        changed[0] = (byte)(changed[0] == (byte)'0' ? '1' : '0');
        Assert.False(Signatures.Verify(changed, sig, Key("ubuntu-cdimage.asc"), UbuntuKey));
        Assert.False(Signatures.Verify(list, sig, Key("linuxmint-iso.asc"), MintKey));             // Mint's key didn't sign Ubuntu's list
        Assert.False(Signatures.Verify(list, sig, Key("ubuntu-cdimage.asc"), MintKey));            // the key isn't the pinned one
        Assert.False(Signatures.Verify(list, [1, 2, 3], Key("ubuntu-cdimage.asc"), UbuntuKey));     // garbage signature
    }

    [Fact]
    public void Checksum_lists_are_read_in_both_styles()
    {
        Assert.Equal("601e30fbf5d97759367c632e2c33630665039b7e2158fd068403da3ccf1bda1f", Checksums.Find(File.ReadAllText(Fixture("ubuntu-SHA256SUMS")), "ubuntu-26.04.1-desktop-amd64.iso"));
        Assert.NotNull(Checksums.Find(File.ReadAllText(Fixture("mint-sha256sum.txt")), "linuxmint-22.3-cinnamon-64bit.iso"));
        Assert.Null(Checksums.Find(File.ReadAllText(Fixture("ubuntu-SHA256SUMS")), "missing.iso"));
    }

    // ---- Resolving the download ------------------------------------------------------------------

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Requests.Add(r); return Task.FromResult(respond(r)); }
    }

    private static InstallerSource Ubuntu => new()
    {
        DistroName = "Ubuntu", File = "ubuntu-26.04.1-desktop-amd64.iso",
        Url = "https://releases.ubuntu.com/26.04.1/ubuntu-26.04.1-desktop-amd64.iso",
        Checksums = "https://releases.ubuntu.com/26.04.1/SHA256SUMS", Signature = "https://releases.ubuntu.com/26.04.1/SHA256SUMS.gpg",
        Key = Key("ubuntu-cdimage.asc"), KeyFingerprint = UbuntuKey,
    };

    [Fact]
    public async Task Ubuntu_download_is_resolved_from_the_signed_list()
    {
        var http = new HttpClient(new FakeHttp(r => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(File.ReadAllBytes(Fixture(r.RequestUri!.AbsolutePath.EndsWith(".gpg") ? "ubuntu-SHA256SUMS.gpg" : "ubuntu-SHA256SUMS"))),
        }));
        var r = await InstallerResolver.ResolveAsync(http, Ubuntu, CancellationToken.None);
        Assert.Equal(("601e30fbf5d97759367c632e2c33630665039b7e2158fd068403da3ccf1bda1f", CheckKind.SignedChecksum), (r.Sha256, r.Check));
    }

    [Fact]
    public async Task A_tampered_list_or_plain_http_is_refused()
    {
        var tampered = new HttpClient(new FakeHttp(r => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(r.RequestUri!.AbsolutePath.EndsWith(".gpg")
                ? File.ReadAllBytes(Fixture("ubuntu-SHA256SUMS.gpg"))
                : System.Text.Encoding.UTF8.GetBytes(new string('a', 64) + " *ubuntu-26.04.1-desktop-amd64.iso\n")),
        }));
        await Assert.ThrowsAsync<InstallerCheckException>(() => InstallerResolver.ResolveAsync(tampered, Ubuntu, CancellationToken.None));
        await Assert.ThrowsAsync<InstallerCheckException>(() => InstallerResolver.ResolveAsync(tampered, Ubuntu with { Url = "http://releases.ubuntu.com/x.iso" }, CancellationToken.None));
    }

    [Fact]
    public async Task Pop_os_comes_from_System76s_api_and_must_point_to_its_iso_server()
    {
        const string json = """{"url":"https://iso.pop-os.org/24.04/amd64/nvidia/28/pop-os_24.04_amd64_nvidia_28.iso","size":3934945280,"sha_sum":"bd867a6b93810769b2189471cfc2d7b6b9c642213c7bf827de511be5f42ce666"}""";
        var source = new InstallerSource { DistroName = "Pop!_OS", Api = "https://api.pop-os.org/builds/24.04/nvidia" };
        var r = await InstallerResolver.ResolveAsync(new HttpClient(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) })), source, CancellationToken.None);
        Assert.Equal(("pop-os_24.04_amd64_nvidia_28.iso", CheckKind.ChecksumOverHttps, 3934945280L), (r.FileName, r.Check, r.Bytes!.Value));
        var elsewhere = json.Replace("iso.pop-os.org", "evil.example");
        await Assert.ThrowsAsync<InstallerCheckException>(() => InstallerResolver.ResolveAsync(new HttpClient(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new StringContent(elsewhere) })), source, CancellationToken.None));
    }

    // ---- Downloading -----------------------------------------------------------------------------

    private static string TempDir() { var d = Path.Combine(Path.GetTempPath(), $"gh-media-{Guid.NewGuid():N}"); Directory.CreateDirectory(d); return d; }

    [Fact]
    public async Task Download_is_checked_resumed_and_a_bad_file_is_deleted()
    {
        var content = RandomNumberGenerator.GetBytes(3_000_000);
        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        var dir = TempDir();
        try
        {
            // Half the file is already there: only the rest is requested.
            await File.WriteAllBytesAsync(Path.Combine(dir, "x.iso.part"), content[..1_000_000]);
            var fake = new FakeHttp(r =>
            {
                var from = (int)(r.Headers.Range?.Ranges.First().From ?? 0);
                return new(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new ByteArrayContent(content[from..]) };
            });
            var path = await InstallerDownloader.DownloadAsync(new HttpClient(fake), new ResolvedInstaller("https://x/x.iso", "x.iso", sha, null, CheckKind.SignedChecksum), dir, null, CancellationToken.None);
            Assert.Equal(content, await File.ReadAllBytesAsync(path));
            Assert.Equal(1_000_000, fake.Requests.Single().Headers.Range!.Ranges.First().From);

            // A file that isn't the official one is deleted.
            var bad = new ResolvedInstaller("https://x/y.iso", "y.iso", new string('0', 64), null, CheckKind.SignedChecksum);
            await Assert.ThrowsAsync<InstallerCheckException>(() => InstallerDownloader.DownloadAsync(new HttpClient(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) })), bad, dir, null, CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(dir, "y.iso")));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- Which disks may be written ---------------------------------------------------------------

    private static DiskInfo Stick(long gb = 16, bool usb = true, bool removable = true, bool windows = false, bool readOnly = false) =>
        new(3, "SanDisk Ultra", gb * 1_000_000_000, usb, removable, windows, readOnly, IsOffline: false);

    [Fact]
    public void Only_usb_sticks_that_dont_hold_windows_and_fit_the_installer_can_be_written()
    {
        Assert.True(UsbSafety.Check(Stick(), 6_482_409_472).CanUse);
        Assert.False(UsbSafety.Check(Stick(usb: false), 1).CanUse);
        Assert.False(UsbSafety.Check(Stick(windows: true), 1).CanUse);
        Assert.False(UsbSafety.Check(Stick(readOnly: true), 1).CanUse);
        Assert.Contains("too small", UsbSafety.Check(Stick(gb: 4), 1).Problem);
        Assert.Contains("too small", UsbSafety.Check(Stick(gb: 8), 9_000_000_000).Problem);
        Assert.Contains("external drive", UsbSafety.Check(Stick(gb: 1000), 1).Problem);
        Assert.Contains("hard drive", UsbSafety.Check(Stick(removable: false), 1).Warning);
        Assert.False(UsbSafety.IsListed(Stick(usb: false)));
        Assert.False(UsbSafety.IsListed(Stick(windows: true)));
    }

    // ---- Writing and reading back -------------------------------------------------------------------

    [Fact]
    public async Task Image_is_written_sector_aligned_and_read_back()
    {
        var image = RandomNumberGenerator.GetBytes(5_000_001); // not a whole number of sectors
        var sha = Convert.ToHexStringLower(SHA256.HashData(image));
        var disk = new MemoryStream(new byte[8_000_000]);
        var stages = new List<string>();
        await ImageWriter.WriteAndVerifyAsync(new MemoryStream(image), image.Length, disk, 512, sha, new SyncProgress(p => stages.Add(p.Stage)), CancellationToken.None);
        Assert.Equal(image, disk.ToArray()[..image.Length]);
        Assert.All(disk.ToArray()[image.Length..(image.Length + 511)], b => Assert.Equal(0, b)); // padding
        Assert.Equal([ImageWriter.Writing, ImageWriter.Verifying], stages.Distinct());
    }

    [Fact]
    public async Task A_stick_that_reads_back_wrong_fails_the_check()
    {
        var image = RandomNumberGenerator.GetBytes(1_048_576);
        var sha = Convert.ToHexStringLower(SHA256.HashData(image));
        await Assert.ThrowsAsync<IOException>(() => ImageWriter.WriteAndVerifyAsync(new MemoryStream(image), image.Length, new FlakyDisk(2_000_000), 512, sha, null, CancellationToken.None));
    }

    [Fact]
    public async Task Writing_can_be_cancelled()
    {
        var image = RandomNumberGenerator.GetBytes(10_000_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ImageWriter.WriteAndVerifyAsync(new MemoryStream(image), image.Length, new MemoryStream(new byte[10_000_000]), 512, "", null, cts.Token));
    }

    private sealed class SyncProgress(Action<MediaProgress> report) : IProgress<MediaProgress> { public void Report(MediaProgress value) => report(value); }

    /// <summary>A disk that flips one bit when read back, like a failing stick.</summary>
    private sealed class FlakyDisk(int size) : MemoryStream(new byte[size])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var bytes = new byte[buffer.Length];
            var n = base.Read(bytes, 0, bytes.Length);
            if (n > 0) bytes[0] ^= 1;
            bytes.AsSpan(0, n).CopyTo(buffer.Span);
            return ValueTask.FromResult(n);
        }
    }

    [Fact]
    public void Every_download_in_the_data_is_complete_and_its_key_matches_the_pinned_fingerprint()
    {
        var data = GreyHaven.Compat.CompatibilityData.Load();
        foreach (var e in data.Distros.Distros.SelectMany(d => d.Editions))
        {
            var dl = Assert.IsType<GreyHaven.Compat.Data.DownloadInfo>(e.Download);
            if (dl.Api is not null) { Assert.StartsWith("https://api.pop-os.org/", dl.Api); continue; }
            Assert.All(new[] { dl.Url, dl.Checksums, dl.Signature }, u => Assert.StartsWith("https://", u));
            Assert.EndsWith(dl.File!, dl.Url);
            Assert.Equal(dl.KeyFingerprint, Signatures.Fingerprint(Key(Path.GetFileName(dl.Key!))));
        }
    }
}
