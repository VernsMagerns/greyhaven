namespace GreyHaven.Core;

/// <summary>Project-wide constants. Change these in one place only.</summary>
public static class GreyHavenInfo
{
    public const string ProductName = "GreyHaven";

    /// <summary>
    /// GitHub "owner/repo" used for contribution issues (milestone 7) and the update check
    /// (milestone 8). Placeholder until the repository exists.
    /// </summary>
    public const string GitHubRepository = "OWNER/greyhaven";

    public static bool IsGitHubRepositoryConfigured => !GitHubRepository.StartsWith("OWNER/", StringComparison.Ordinal);

    public static string Version =>
        typeof(GreyHavenInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
