using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using GreyHaven.Core;

namespace GreyHaven.Compat.Online;

/// <summary>
/// The HTTP client GreyHaven uses for all online requests. It identifies itself honestly and
/// connects with "Happy Eyeballs" (RFC 8305 style): if a site has both IPv6 and IPv4 addresses,
/// it tries them in parallel with a short stagger and uses whichever connects first. Without
/// this, a PC with broken IPv6 waits ~30 seconds per request before falling back to IPv4.
/// </summary>
public static class GreyHavenHttp
{
    private static readonly TimeSpan AttemptStagger = TimeSpan.FromMilliseconds(250);

    public static HttpClient Create(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"GreyHaven/{GreyHavenInfo.Version}");
        return client;
    }

    internal static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = IPAddress.TryParse(endpoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        var ordered = Interleave(addresses);
        if (ordered.Count == 0) throw new HttpRequestException($"No addresses found for {endpoint.Host}.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = new List<Task<Socket>>();
        var errors = new List<Exception>();
        var next = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (next < ordered.Count) pending.Add(ConnectOneAsync(ordered[next++], endpoint.Port, cts.Token));
            if (pending.Count == 0)
                throw new HttpRequestException($"Couldn't connect to {endpoint.Host}.", new AggregateException(errors));

            // Wait for an attempt to finish, or (if more addresses remain) the stagger delay.
            var waitOn = new List<Task>(pending);
            if (next < ordered.Count) waitOn.Add(Task.Delay(AttemptStagger, cts.Token));
            var finished = await Task.WhenAny(waitOn).ConfigureAwait(false);
            if (finished is not Task<Socket> attempt) continue; // stagger elapsed: start the next address

            pending.Remove(attempt);
            if (attempt.IsCompletedSuccessfully)
            {
                cts.Cancel();
                foreach (var loser in pending)
                    _ = loser.ContinueWith(t => { if (t.IsCompletedSuccessfully) t.Result.Dispose(); }, TaskScheduler.Default);
                return new NetworkStream(attempt.Result, ownsSocket: true);
            }
            errors.Add(attempt.Exception?.InnerException ?? new HttpRequestException("Connection failed."));
        }
    }

    private static async Task<Socket> ConnectOneAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Alternates address families, IPv6 first, as RFC 8305 recommends.</summary>
    internal static List<IPAddress> Interleave(IEnumerable<IPAddress> addresses)
    {
        var v6 = new Queue<IPAddress>(addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        var v4 = new Queue<IPAddress>(addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork));
        var result = new List<IPAddress>();
        while (v6.Count > 0 || v4.Count > 0)
        {
            if (v6.Count > 0) result.Add(v6.Dequeue());
            if (v4.Count > 0) result.Add(v4.Dequeue());
        }
        return result;
    }
}
