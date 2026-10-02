using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace SelectAI.Core.Services;

public sealed class UpdateInfo
{
    public bool UpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}

public static class UpdateCheckerService
{
    private const string GitHubApiLatest = "https://api.github.com/repos/bharath-0814/SelectAI/releases/latest";
    private static readonly HttpClient HttpClient = new();

    static UpdateCheckerService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "SelectAI-Updater");
        HttpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.1.0";
        }
    }

    public static async Task<UpdateInfo> CheckForUpdateAsync()
    {
        try
        {
            var response = await HttpClient.GetStringAsync(GitHubApiLatest);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var releaseNotes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            string downloadUrl = "https://github.com/bharath-0814/SelectAI/releases/latest";
            if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                downloadUrl = assets[0].GetProperty("browser_download_url").GetString() ?? downloadUrl;
            }

            var current = CurrentVersion.TrimStart('v');
            var latest = tagName.TrimStart('v');

            bool hasUpdate = IsNewerVersion(latest, current);

            return new UpdateInfo
            {
                UpdateAvailable = hasUpdate,
                LatestVersion = tagName,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check for updates: {ex.Message}");
            return new UpdateInfo { UpdateAvailable = false };
        }
    }

    private static bool IsNewerVersion(string latestStr, string currentStr)
    {
        if (Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current))
        {
            return latest > current;
        }
        return !string.Equals(latestStr, currentStr, StringComparison.OrdinalIgnoreCase);
    }
}
