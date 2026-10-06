using System.Net;
using System.Net.Sockets;
using System.Text;
using GreyHaven.Compat.Online;

namespace GreyHaven.Tests.Compat;

public class GreyHavenHttpTests
{
    [Fact]
    public void Address_families_are_interleaved_ipv6_first()
    {
        var a6 = IPAddress.Parse("2001:db8::1");
        var b6 = IPAddress.Parse("2001:db8::2");
        var a4 = IPAddress.Parse("192.0.2.1");
        Assert.Equal([a6, a4, b6], GreyHavenHttp.Interleave([a4, a6, b6]));
    }

    [Fact]
    public async Task Connects_when_only_ipv4_is_listening()
    {
        // "localhost" resolves to ::1 and 127.0.0.1; only IPv4 accepts, so the IPv6 attempt
        // must fail over rather than block the request.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);
            await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"u8.ToArray());
        });

        try
        {
            using var http = GreyHavenHttp.Create(TimeSpan.FromSeconds(10));
            var body = await http.GetStringAsync($"http://localhost:{port}/");
            Assert.Equal("ok", body);
            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void Client_identifies_itself()
    {
        using var http = GreyHavenHttp.Create(TimeSpan.FromSeconds(1));
        Assert.StartsWith("GreyHaven/", http.DefaultRequestHeaders.UserAgent.ToString());
    }
}
