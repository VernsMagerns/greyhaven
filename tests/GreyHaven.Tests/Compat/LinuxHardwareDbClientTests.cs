using System.Net;
using System.Security.Authentication;
using GreyHaven.Compat.Online;
using GreyHaven.Core.Hardware;
using Microsoft.Extensions.Time.Testing;

namespace GreyHaven.Tests.Compat;

public class LinuxHardwareDbClientTests : IDisposable
{
    // Trimmed copy of the structure of a real device page (2026-10-05).
    private const string DevicePage = """
        <html><head><title> Intel Wi-Fi 6 AX200 </title></head><body>
        <h2 class=''>Kernel Drivers</h2>
        <a name='status'></a> <h2>Status (12050)</h2> The device is a part of the following computers:
        <table>
        <tr><td>desktop</td><td>Ubuntu 24.04</td><td class='works'>works</td></tr>
        <tr><td>notebook</td><td>Fedora 40</td><td class='works'>works</td></tr>
        <tr><td>notebook</td><td>Ubuntu 20.04</td><td class='failed'>failed</td></tr>
        <tr><td>desktop</td><td>Mint 22</td><td class='detected'>detected</td></tr>
        <tr><td>desktop</td><td>Arch</td><td class='malfunc'>malfunc</td></tr>
        </table></body></html>
        """;

    private readonly string _cache = Path.Combine(Path.GetTempPath(), $"gh-lhdb-{Guid.NewGuid():N}");
    private static readonly DeviceId Ax200 = new(DeviceBus.Pci, "8086", "2723", "8086", "0084");

    public void Dispose()
    {
        if (Directory.Exists(_cache)) Directory.Delete(_cache, recursive: true);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Html(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public void Page_url_contains_only_device_ids()
    {
        Assert.Equal("https://linux-hardware.org/?id=pci:8086-2723-8086-0084", LinuxHardwareDbClient.PageUrl(Ax200));
        Assert.Equal("https://linux-hardware.org/?id=usb:8087-0026", LinuxHardwareDbClient.PageUrl(new DeviceId(DeviceBus.Usb, "8087", "0026")));
        Assert.Null(LinuxHardwareDbClient.PageUrl(new DeviceId(DeviceBus.HdAudio, "10ec", "0287")));
    }

    [Fact]
    public void Parses_name_total_and_status_counts()
    {
        var r = LinuxHardwareDbClient.Parse(DevicePage, "u", DateTimeOffset.UnixEpoch);
        Assert.Equal(CommunityStatus.Found, r.Status);
        Assert.Equal("Intel Wi-Fi 6 AX200", r.DeviceName);
        Assert.Equal(12050, r.TotalComputers);
        Assert.Equal((2, 1, 1, 1), (r.Works, r.Detected, r.Failed, r.Malfunctioning));
    }

    [Fact]
    public async Task Successful_lookup_is_cached_and_not_fetched_again()
    {
        var handler = new FakeHandler(_ => Html(DevicePage));
        var client = new LinuxHardwareDbClient(new HttpClient(handler), _cache);

        var first = await client.LookupAsync(Ax200);
        var second = await new LinuxHardwareDbClient(new HttpClient(handler), _cache).LookupAsync(Ax200);

        Assert.Equal(CommunityStatus.Found, first.Status);
        Assert.Equal(CommunityStatus.Found, second.Status);
        Assert.Single(handler.Requests);
        Assert.Equal("https://linux-hardware.org/?id=pci:8086-2723-8086-0084", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task Board_specific_page_falls_back_to_the_general_device_page()
    {
        // Few people share the exact same board, so pci:10de-1ad9-1462-c757 may have no page
        // while pci:10de-1ad9 does.
        var time = new FakeTimeProvider();
        var handler = new FakeHandler(r => r.RequestUri!.Query.Count(ch => ch == '-') > 1 ? new HttpResponseMessage(HttpStatusCode.NotFound) : Html(DevicePage));
        var lookup = new LinuxHardwareDbClient(new HttpClient(handler), null, time)
            .LookupAsync(new DeviceId(DeviceBus.Pci, "10de", "1ad9", "1462", "c757"));
        time.Advance(LinuxHardwareDbClient.MinInterval);
        var r = await lookup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CommunityStatus.Found, r.Status);
        Assert.Equal(["https://linux-hardware.org/?id=pci:10de-1ad9-1462-c757", "https://linux-hardware.org/?id=pci:10de-1ad9"], handler.Requests.Select(u => u.ToString()));
    }

    [Fact]
    public async Task Requests_are_at_least_ten_seconds_apart()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-10-05T00:00:00Z"));
        var handler = new FakeHandler(_ => Html(DevicePage));
        var client = new LinuxHardwareDbClient(new HttpClient(handler), cacheDirectory: null, time);

        await client.LookupAsync(Ax200);
        var second = client.LookupAsync(new DeviceId(DeviceBus.Usb, "8087", "0026"));
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Rate_limit_stops_further_requests_for_the_session()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var time = new FakeTimeProvider();
        var client = new LinuxHardwareDbClient(new HttpClient(handler), null, time);

        var r1 = await client.LookupAsync(Ax200);
        var r2 = await client.LookupAsync(new DeviceId(DeviceBus.Usb, "8087", "0026"));

        Assert.Equal(CommunityStatus.Unavailable, r1.Status);
        Assert.Contains("rate limit", r1.Reason);
        Assert.Equal(CommunityStatus.Unavailable, r2.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Certificate_errors_are_reported_not_bypassed()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("ssl", new AuthenticationException("certificate expired")));
        var r = await new LinuxHardwareDbClient(new HttpClient(handler), null).LookupAsync(Ax200);

        Assert.Equal(CommunityStatus.Unavailable, r.Status);
        Assert.Contains("certificate", r.Reason);
    }

    [Fact]
    public async Task Offline_is_reported_clearly_and_not_cached()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("No such host is known."));
        var r = await new LinuxHardwareDbClient(new HttpClient(handler), _cache).LookupAsync(Ax200);

        Assert.Equal(CommunityStatus.Unavailable, r.Status);
        Assert.Contains("offline", r.Reason);
        Assert.False(Directory.Exists(_cache) && Directory.EnumerateFiles(_cache).Any());
    }

    [Fact]
    public async Task Not_found_pages_are_reported()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var r = await new LinuxHardwareDbClient(new HttpClient(handler), null).LookupAsync(Ax200);
        Assert.Equal(CommunityStatus.NotFound, r.Status);
    }

    [Fact]
    public async Task Buses_without_device_pages_are_not_queried()
    {
        var handler = new FakeHandler(_ => Html(DevicePage));
        var r = await new LinuxHardwareDbClient(new HttpClient(handler), null).LookupAsync(new DeviceId(DeviceBus.Acpi, "QCOM0C36", ""));
        Assert.Equal(CommunityStatus.NotApplicable, r.Status);
        Assert.Empty(handler.Requests);
    }
}
