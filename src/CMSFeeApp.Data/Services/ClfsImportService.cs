using System.Globalization;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Parses CMS Clinical Laboratory Fee Schedule (CLFS) CSV files and imports
/// records into the database.
/// </summary>
public class ClfsImportService : IFeeImporter
{
    private readonly ClfsRepository _repository;
    private readonly ImportLogRepository _importLogRepository;

    private static readonly IReadOnlyList<string> _supportedExtensions = [".csv", ".txt"];

    public FeeScheduleType ScheduleType => FeeScheduleType.ClinicalLab;
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public ClfsImportService(ClfsRepository repository, ImportLogRepository importLogRepository)
    {
        _repository = repository;
        _importLogRepository = importLogRepository;
    }

    public async Task<ImportResult> ImportFromFileAsync(string filePath, int year, CancellationToken ct = default)
    {
        try
        {
            var fees = await Task.Run(() => ParseFile(filePath, year), ct);
            if (fees.Count == 0)
            {
                var msg = "No records were parsed from the file. Verify the file format.";
                _importLogRepository.LogImport(FeeScheduleType.ClinicalLab, year, filePath, 0, false, msg);
                return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = msg };
            }

            await Task.Run(() => _repository.InsertFees(fees), ct);

            _importLogRepository.LogImport(FeeScheduleType.ClinicalLab, year, filePath, fees.Count, true);
            return new ImportResult { Success = true, RecordsImported = fees.Count };
        }
        catch (Exception ex)
        {
            _importLogRepository.LogImport(FeeScheduleType.ClinicalLab, year, filePath, 0, false, ex.Message);
            return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = ex.Message };
        }
    }

    internal IReadOnlyList<ClfsFee> ParseFile(string filePath, int year)
    {
        var delimiter = DmeposImportService.DetectDelimiter(filePath);
        return ParseCsv(filePath, year, delimiter);
    }

    public IReadOnlyList<ClfsFee> ParseCsv(string filePath, int year, char delimiter = ',')
    {
        var records = new List<ClfsFee>();
        var importedAt = DateTime.UtcNow;

        int headerLineIdx;
        using (var scan = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
        {
            (headerLineIdx, _) = FindHeaderLine(scan);
        }
        if (headerLineIdx < 0) return records;

        using var stream = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        for (int i = 0; i < headerLineIdx; i++)
            stream.ReadLine();

        var headerRow = stream.ReadLine();
        if (headerRow == null) return records;
        var headers = DmeposImportService.ParseCsvLine(headerRow, delimiter)
            .Select(h => h.Trim())
            .ToArray();

        string? line;
        while ((line = stream.ReadLine()) != null)
        {
            var values = DmeposImportService.ParseCsvLine(line, delimiter);
            if (values.Length == 0 || values.All(string.IsNullOrWhiteSpace))
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
                row[headers[i]] = (values[i] ?? "").Trim();

            var hcpcs = (GetValue(row, "hcpcs", "hcpcs_code", "hcpcs code", "hcpcs_cd") ?? "").ToUpperInvariant().Trim();
            if (string.IsNullOrEmpty(hcpcs)) continue;

            var description = GetValue(row, "description", "long_description", "short_description") ?? "";

            var paymentLimit = DmeposImportService.ParseAmount(
                GetValue(row, "payment limit", "payment_limit", "national limit", "national_limit", "limit"));

            var modifier = (GetValue(row, "modifier", "mod") ?? "").Trim();
            if (string.IsNullOrEmpty(modifier)) modifier = null;

            records.Add(new ClfsFee
            {
                HcpcsCode = hcpcs,
                Description = description,
                Year = year,
                PaymentLimit = paymentLimit ?? 0m,
                Modifier = modifier,
                DataSource = "file_import",
                ImportedAt = importedAt
            });
        }

        return records;
    }

    private static (int index, string? line) FindHeaderLine(StreamReader reader)
    {
        int idx = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("hcpcs") && (lower.Contains("description") || lower.Contains("limit") || lower.Contains("payment")))
                return (idx, line);
            idx++;
        }
        return (-1, null);
    }

    private static string? GetValue(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
            if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        return null;
    }
}
