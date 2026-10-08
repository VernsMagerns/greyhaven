using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace GreyHaven.Media;

/// <summary>Checksum lists like Ubuntu's SHA256SUMS ("&lt;hash&gt; *&lt;file&gt;") and Mint's sha256sum.txt ("&lt;hash&gt;  &lt;file&gt;").</summary>
public static class Checksums
{
    /// <summary>The SHA-256 (lower case) listed for a file name, or null.</summary>
    public static string? Find(string list, string fileName)
    {
        foreach (var raw in list.Split('\n'))
        {
            var line = raw.Trim();
            var space = line.IndexOf(' ');
            if (space != 64) continue;
            var name = line[(space + 1)..].TrimStart(' ', '*');
            if (name == fileName && line[..64].All(Uri.IsHexDigit)) return line[..64].ToLowerInvariant();
        }
        return null;
    }

    public static async Task<string> Sha256Async(Stream stream, long? length, IProgress<long>? progress, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[4 << 20];
        long done = 0;
        while (length is null || done < length)
        {
            var want = length is { } l ? (int)Math.Min(buffer.Length, l - done) : buffer.Length;
            var n = await stream.ReadAsync(buffer.AsMemory(0, want), ct).ConfigureAwait(false);
            if (n == 0) break;
            sha.AppendData(buffer, 0, n);
            done += n;
            progress?.Report(done);
        }
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}

/// <summary>Checks a detached OpenPGP signature against a pinned key (BouncyCastle, MIT licence).</summary>
public static class Signatures
{
    /// <summary>
    /// True when <paramref name="signature"/> is a valid signature of <paramref name="data"/> made by
    /// the key in <paramref name="armoredKey"/>, and that key's fingerprint is exactly
    /// <paramref name="expectedFingerprint"/> (so a swapped key file is refused too).
    /// </summary>
    public static bool Verify(byte[] data, byte[] signature, string armoredKey, string expectedFingerprint)
    {
        var expected = expectedFingerprint.Replace(" ", "").ToUpperInvariant();
        PgpPublicKeyRingBundle keys;
        try { keys = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.ASCII.GetBytes(armoredKey)))); }
        catch (Exception ex) when (ex is IOException or PgpException or Org.BouncyCastle.Bcpg.UnsupportedPacketVersionException) { return false; }

        foreach (var sig in ReadSignatures(signature))
        {
            var ring = keys.GetPublicKeyRing(sig.KeyId);
            var key = ring?.GetPublicKey(sig.KeyId);
            if (ring is null || key is null) continue;
            if (Convert.ToHexString(ring.GetPublicKey().GetFingerprint()) != expected) continue; // only the pinned key
            if (key.HasRevocation()) continue;
            try
            {
                sig.InitVerify(key);
                sig.Update(data);
                if (sig.Verify()) return true;
            }
            catch (PgpException) { }
        }
        return false;
    }

    /// <summary>The main key's fingerprint (upper-case hex) in an armored key file, or null if it can't be read.</summary>
    public static string? Fingerprint(string armoredKey)
    {
        try
        {
            var keys = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.ASCII.GetBytes(armoredKey))));
            return keys.GetKeyRings().Select(r => Convert.ToHexString(r.GetPublicKey().GetFingerprint())).SingleOrDefault();
        }
        catch (Exception ex) when (ex is IOException or PgpException or InvalidOperationException) { return null; }
    }

    private static IEnumerable<PgpSignature> ReadSignatures(byte[] bytes)
    {
        PgpObjectFactory factory;
        try { factory = new PgpObjectFactory(PgpUtilities.GetDecoderStream(new MemoryStream(bytes))); }
        catch (IOException) { yield break; }
        while (true)
        {
            PgpObject? obj;
            try { obj = factory.NextPgpObject(); }
            catch (Exception ex) when (ex is IOException or PgpException or Org.BouncyCastle.Bcpg.UnsupportedPacketVersionException) { yield break; }
            if (obj is null) yield break;
            if (obj is PgpCompressedData compressed)
            {
                factory = new PgpObjectFactory(compressed.GetDataStream());
                continue;
            }
            if (obj is PgpSignatureList list)
                for (var i = 0; i < list.Count; i++) yield return list[i];
        }
    }
}
