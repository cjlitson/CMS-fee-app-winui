using System.IO.Compression;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Downloads the CMS DMEPOS fee schedule ZIP for a given year, extracts it,
/// parses it, and stores the data in the local database.
/// Ported from the Python app's core/cms_downloader.py.
///
/// URL discovery layers (in order):
///   1. TODO: RSS feed scraping (follow-up)
///   2. TODO: HTML page scraping (follow-up)
///   3. Pattern-based URL templates from previously successful downloads
///   4. Hardcoded fallback URL templates (known working patterns)
/// </summary>
public class DmeposCmsSyncService : ICmsSyncService
{
    private readonly HttpClient _httpClient;
    private readonly DmeposImportService _importService;
    private readonly DmeposRepository _dmeposRepository;
    private readonly ImportLogRepository _importLogRepository;

    public FeeScheduleType ScheduleType => FeeScheduleType.Dmepos;

    // CMS URL templates for DMEPOS fee schedule ZIPs.
    // {year2d} = 2-digit year (e.g. "25" for 2025); {year} = 4-digit year.
    private static readonly string[] UrlTemplates =
    [
        // No-quarter variant — CMS publishes this for the initial/only release
        "https://www.cms.gov/files/zip/dme{year2d}.zip",
        // Quarterly patterns — current CMS convention (most-recent quarter first)
        "https://www.cms.gov/files/zip/dme{year2d}-d.zip",
        "https://www.cms.gov/files/zip/dme{year2d}-c.zip",
        "https://www.cms.gov/files/zip/dme{year2d}-b.zip",
        "https://www.cms.gov/files/zip/dme{year2d}-a.zip",
        // No-hyphen variants
        "https://www.cms.gov/files/zip/dme{year2d}d.zip",
        "https://www.cms.gov/files/zip/dme{year2d}c.zip",
        "https://www.cms.gov/files/zip/dme{year2d}b.zip",
        "https://www.cms.gov/files/zip/dme{year2d}a.zip",
        // Legacy patterns
        "https://www.cms.gov/files/zip/{year}-dmepos-fee-schedule.zip",
        "https://www.cms.gov/files/zip/dmepos-{year}-fee-schedule.zip",
    ];

    // File name keywords to skip (non-data files in the ZIP)
    private static readonly string[] SkipKeywords =
        ["addenda", "readme", "read_me", "guide", "instructions", "template", "modifier"];

    // Keywords that indicate the file is not the main fee schedule data
    private static readonly string[] AuxiliaryKeywords =
        ["rural", "zip", "pricing_indicator", "pricingindicator", "hcpcs_code_only"];

    public DmeposCmsSyncService(
        HttpClient httpClient,
        DmeposImportService importService,
        DmeposRepository dmeposRepository,
        ImportLogRepository importLogRepository)
    {
        _httpClient = httpClient;
        _importService = importService;
        _dmeposRepository = dmeposRepository;
        _importLogRepository = importLogRepository;
    }

    public async Task<ImportResult> SyncFromCmsAsync(int year, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        progress?.Report($"Starting CMS DMEPOS sync for {year}…");

        try
        {
            var zipBytes = await TryDownloadZipAsync(year, progress, ct);
            if (zipBytes == null)
            {
                var msg = $"Could not find a DMEPOS fee schedule ZIP for {year} at CMS.gov. " +
                          "Try importing manually using File → Import.";
                _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, null, 0, false, msg);
                return new ImportResult { Success = false, ErrorMessage = msg };
            }

            progress?.Report("Extracting archive…");
            var (dataFileName, dataBytes) = ExtractMainCsvFromZip(zipBytes);
            progress?.Report($"Selected ZIP entry: {dataFileName}");

            var tmpPath = Path.Combine(Path.GetTempPath(), $"cms_dmepos_{year}_{Guid.NewGuid():N}.csv");
            try
            {
                await File.WriteAllBytesAsync(tmpPath, dataBytes, ct);

                progress?.Report($"Parsing {dataFileName}…");
                var fees = await Task.Run(() => _importService.ParseFile(tmpPath, year), ct);

                if (fees.Count == 0)
                {
                    var msg = $"Parsed 0 records from {dataFileName} for {year}. Aborting to avoid wiping existing data.";
                    _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, dataFileName, 0, false, msg);
                    return new ImportResult { Success = false, ErrorMessage = msg };
                }

                progress?.Report($"Parsed {fees.Count:N0} records. Replacing existing data for {year}…");

                // Replace semantics: delete existing CMS-downloaded rows, then insert fresh
                await Task.Run(() =>
                {
                    _dmeposRepository.DeleteFeesByYearState(year, dataSource: "cms_download");
                    foreach (var f in fees) f.DataSource = "cms_download";
                    _dmeposRepository.InsertFees(fees);
                }, ct);

                _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, dataFileName, fees.Count, true);
                progress?.Report($"CMS DMEPOS sync complete: {fees.Count:N0} records imported for {year}.");
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
            _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, null, 0, false, ex.Message);
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

        var entry = SelectMainDmeposEntry(zip, allNames)
            ?? throw new InvalidDataException(
                $"No CSV data file found in the downloaded ZIP. Contents: {string.Join(", ", allNames)}");

        using var entryStream = entry.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        return (entry.Name, ms.ToArray());
    }

    private static ZipArchiveEntry? SelectMainDmeposEntry(ZipArchive zip, List<string> allNames)
    {
        static bool IsSkipped(string name) =>
            SkipKeywords.Any(kw => name.ToLowerInvariant().Contains(kw));

        static bool HasAuxKeyword(string name) =>
            AuxiliaryKeywords.Any(kw => name.ToLowerInvariant().Contains(kw));

        // Tier 1: starts with "dmepos", .csv, not skipped, no exclusion keywords
        var tier1 = allNames.Where(n =>
            Path.GetFileName(n).StartsWith("dmepos", StringComparison.OrdinalIgnoreCase) &&
            n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            !IsSkipped(n) && !HasAuxKeyword(n)).ToList();
        if (tier1.Count > 0)
        {
            tier1.Sort((a, b) => (int)(zip.GetEntry(b)!.Length - zip.GetEntry(a)!.Length));
            return zip.GetEntry(tier1[0]);
        }

        // Tier 2: contains "dmepos", .csv, no auxiliary keyword
        var tier2 = allNames.Where(n =>
            n.ToLowerInvariant().Contains("dmepos") &&
            n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            !IsSkipped(n) && !HasAuxKeyword(n)).ToList();
        if (tier2.Count > 0)
        {
            tier2.Sort((a, b) => (int)(zip.GetEntry(b)!.Length - zip.GetEntry(a)!.Length));
            return zip.GetEntry(tier2[0]);
        }

        // Tier 3: any non-skipped .csv, largest first
        var tier3 = allNames.Where(n =>
            n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && !IsSkipped(n)).ToList();
        if (tier3.Count > 0)
        {
            tier3.Sort((a, b) => (int)(zip.GetEntry(b)!.Length - zip.GetEntry(a)!.Length));
            return zip.GetEntry(tier3[0]);
        }

        return null;
    }
}
