namespace GreyHaven.Core;

/// <summary>Project-wide constants. Change these in one place only.</summary>
public static class GreyHavenInfo
{
    public const string ProductName = "GreyHaven";

    /// <summary>
    /// GitHub "owner/repo" used for contribution issues (milestone 7) and the update check
    /// (milestone 8). tools/release-check.sh fails if this is ever a placeholder.
    /// </summary>
    public const string GitHubRepository = "VernsMagerns/greyhaven";

    /// <summary>
    /// Whether people other than the maintainer can open the repository. While it's private,
    /// GreyHaven links its bundled copies of its own guides instead of github.com, which
    /// would show everyone else a "not found" page. Set to true when the repository goes public.
    /// </summary>
    public const bool IsRepositoryPublic = false;

    /// <summary>
    /// The public phone guide that the QR code opens: the Cloudflare Pages project deployed by
    /// .github/workflows/phone-guide.yml. Always https and never a local address (a test checks).
    /// </summary>
    public const string PhoneGuideSite = "https://greyhaven-guide.pages.dev/";

    public static bool IsGitHubRepositoryConfigured => !GitHubRepository.StartsWith("OWNER/", StringComparison.Ordinal);

    /// <summary>Links to the repository work for everyone: it's configured and public.</summary>
    public static bool CanLinkToRepository => IsGitHubRepositoryConfigured && IsRepositoryPublic;

    public static string Version =>
        typeof(GreyHavenInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
