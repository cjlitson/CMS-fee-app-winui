namespace CMSFeeApp.Core.Models;

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string? LatestVersion { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? ReleaseName { get; set; }
}
