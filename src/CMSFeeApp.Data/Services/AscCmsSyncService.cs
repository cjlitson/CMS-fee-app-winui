using System.IO.Compression;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Downloads the CMS Ambulatory Surgical Center (ASC) fee schedule for a given year,
/// extracts it, parses it, and stores the data in the local database.
/// </summary>
public class AscCmsSyncService : ICmsSyncService
{
    private readonly HttpClient _httpClient;
    private readonly AscImportService _importService;
    private readonly AscRepository _ascRepository;
    private readonly ImportLogRepository _importLogRepository;

    public FeeScheduleType ScheduleType => FeeScheduleType.Asc;

    private static readonly string[] UrlTemplates =
    [
        "https://www.cms.gov/files/zip/asc{year2d}.zip",
        "https://www.cms.gov/files/zip/asc{year}.zip",
        "https://www.cms.gov/files/zip/{year}-asc-fee-schedule.zip",
        "https://www.cms.gov/files/zip/asc-fee-schedule-{year}.zip",
        "https://www.cms.gov/files/zip/cy{year}-asc-fee-schedule.zip",
        "https://www.cms.gov/files/zip/{year}-ambulatory-surgical-center.zip",
    ];

    private static readonly string[] SkipKeywords =
        ["addenda", "readme", "read_me", "guide", "instructions", "template"];

    public AscCmsSyncService(
        HttpClient httpClient,
        AscImportService importService,
        AscRepository ascRepository,
        ImportLogRepository importLogRepository)
    {
        _httpClient = httpClient;
        _importService = importService;
        _ascRepository = ascRepository;
        _importLogRepository = importLogRepository;
    }

    public async Task<ImportResult> SyncFromCmsAsync(int year, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report($"Starting CMS ASC sync for {year}…");

        try
        {
            var zipBytes = await TryDownloadZipAsync(year, progress, ct);
            if (zipBytes == null)
            {
                var msg = $"Could not find an ASC fee schedule file for {year} at CMS.gov. " +
                          "Try importing manually using File → Import.";
                _importLogRepository.LogImport(FeeScheduleType.Asc, year, null, 0, false, msg);
                return new ImportResult { Success = false, ErrorMessage = msg };
            }

            progress?.Report("Extracting archive…");
            var (dataFileName, dataBytes) = ExtractMainCsvFromZip(zipBytes);
            progress?.Report($"Selected ZIP entry: {dataFileName}");

            var tmpPath = Path.Combine(Path.GetTempPath(), $"cms_asc_{year}_{Guid.NewGuid():N}.csv");
            try
            {
                await File.WriteAllBytesAsync(tmpPath, dataBytes, ct);

                progress?.Report($"Parsing {dataFileName}…");
                var fees = await Task.Run(() => _importService.ParseFile(tmpPath, year), ct);

                if (fees.Count == 0)
                {
                    var msg = $"Parsed 0 records from {dataFileName} for {year}. Aborting to avoid wiping existing data.";
                    _importLogRepository.LogImport(FeeScheduleType.Asc, year, dataFileName, 0, false, msg);
                    return new ImportResult { Success = false, ErrorMessage = msg };
                }

                progress?.Report($"Parsed {fees.Count:N0} records. Replacing existing data for {year}…");

                await Task.Run(() =>
                {
                    _ascRepository.DeleteFeesByYear(year, dataSource: "cms_download");
                    foreach (var f in fees) f.DataSource = "cms_download";
                    _ascRepository.InsertFees(fees);
                }, ct);

                _importLogRepository.LogImport(FeeScheduleType.Asc, year, dataFileName, fees.Count, true);
                progress?.Report($"CMS ASC sync complete: {fees.Count:N0} records imported for {year}.");
                return new ImportResult { Success = true, RecordsImported = fees.Count };
            }
            finally
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
        catch (OperationCanceledException)
        {
            return new ImportResult { Success = false, ErrorMessage = "Sync was cancelled." };
        }
        catch (Exception ex)
        {
            _importLogRepository.LogImport(FeeScheduleType.Asc, year, null, 0, false, ex.Message);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<byte[]?> TryDownloadZipAsync(int year, IProgress<string>? progress, CancellationToken ct)
    {
        var year2d = year.ToString()[^2..];
        var candidates = UrlTemplates
            .Select(t => t.Replace("{year2d}", year2d).Replace("{year}", year.ToString()))
            .ToList();

        foreach (var url in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                progress?.Report($"Trying: {url}");
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) continue;

                progress?.Report($"Downloading from {url}…");
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > 0)
                {
                    progress?.Report($"Downloaded {bytes.Length / 1024:N0} KB from {url}");
                    return bytes;
                }
            }
            catch (HttpRequestException)
            {
                // Try next candidate
            }
        }

        return null;
    }

    private static (string fileName, byte[] data) ExtractMainCsvFromZip(byte[] zipBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        var allNames = zip.Entries.Select(e => e.FullName).ToList();

        static bool IsSkipped(string name) =>
            SkipKeywords.Any(kw => name.ToLowerInvariant().Contains(kw));

        // Tier 1: contains "asc", .csv, not skipped
        var tier1 = allNames.Where(n =>
            n.ToLowerInvariant().Contains("asc") &&
            n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            !IsSkipped(n)).ToList();
        if (tier1.Count > 0)
        {
            tier1.Sort((a, b) => (int)(zip.GetEntry(b)!.Length - zip.GetEntry(a)!.Length));
            var entry1 = zip.GetEntry(tier1[0])!;
            using var s1 = entry1.Open();
            using var ms1 = new MemoryStream();
            s1.CopyTo(ms1);
            return (entry1.Name, ms1.ToArray());
        }

        // Tier 2: any non-skipped .csv, largest first
        var tier2 = allNames.Where(n =>
            n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && !IsSkipped(n)).ToList();
        if (tier2.Count > 0)
        {
            tier2.Sort((a, b) => (int)(zip.GetEntry(b)!.Length - zip.GetEntry(a)!.Length));
            var entry2 = zip.GetEntry(tier2[0])!;
            using var s2 = entry2.Open();
            using var ms2 = new MemoryStream();
            s2.CopyTo(ms2);
            return (entry2.Name, ms2.ToArray());
        }

        throw new InvalidDataException(
            $"No CSV data file found in the downloaded ZIP. Contents: {string.Join(", ", allNames)}");
    }
}
