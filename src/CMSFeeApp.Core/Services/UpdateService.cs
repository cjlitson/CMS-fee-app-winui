using System.Reflection;
using System.Text.Json;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Services;

public class UpdateService : IUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/cjlitson/CMS-fee-app-winui/releases/latest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public UpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CMSFeeApp/1.0");
    }

    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(GitHubApiUrl, cancellationToken);
            var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(response, JsonOptions);

            if (releaseInfo is null)
                return new UpdateInfo { IsUpdateAvailable = false };

            var currentVersion = GetCurrentVersion();
            var latestVersion = releaseInfo.TagName?.TrimStart('v');

            if (latestVersion is null)
                return new UpdateInfo { IsUpdateAvailable = false };

            var isNewer = IsVersionNewer(latestVersion, currentVersion);

            return new UpdateInfo
            {
                IsUpdateAvailable = isNewer,
                LatestVersion = latestVersion,
                ReleaseUrl = releaseInfo.HtmlUrl,
                ReleaseName = releaseInfo.Name
            };
        }
        catch
        {
            return new UpdateInfo { IsUpdateAvailable = false };
        }
    }

    private static string GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static bool IsVersionNewer(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(latestVersion, out var latest) &&
            Version.TryParse(currentVersion, out var current))
        {
            return latest > current;
        }
        return false;
    }

    private sealed class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
