using System.Globalization;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Parses CMS Ambulatory Surgical Center (ASC) fee schedule CSV files and imports
/// records into the database.
/// </summary>
public class AscImportService : IFeeImporter
{
    private readonly AscRepository _repository;
    private readonly ImportLogRepository _importLogRepository;

    private static readonly IReadOnlyList<string> _supportedExtensions = [".csv", ".txt"];

    public FeeScheduleType ScheduleType => FeeScheduleType.Asc;
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public AscImportService(AscRepository repository, ImportLogRepository importLogRepository)
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
                _importLogRepository.LogImport(FeeScheduleType.Asc, year, filePath, 0, false, msg);
                return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = msg };
            }

            await Task.Run(() => _repository.InsertFees(fees), ct);

            _importLogRepository.LogImport(FeeScheduleType.Asc, year, filePath, fees.Count, true);
            return new ImportResult { Success = true, RecordsImported = fees.Count };
        }
        catch (Exception ex)
        {
            _importLogRepository.LogImport(FeeScheduleType.Asc, year, filePath, 0, false, ex.Message);
            return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = ex.Message };
        }
    }

    internal IReadOnlyList<AscFee> ParseFile(string filePath, int year)
    {
        var delimiter = DmeposImportService.DetectDelimiter(filePath);
        return ParseCsv(filePath, year, delimiter);
    }

    public IReadOnlyList<AscFee> ParseCsv(string filePath, int year, char delimiter = ',')
    {
        var records = new List<AscFee>();
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

            var paymentRate = DmeposImportService.ParseAmount(
                GetValue(row, "payment rate", "payment_rate", "asc payment", "asc_payment", "rate"));

            records.Add(new AscFee
            {
                HcpcsCode = hcpcs,
                Description = description,
                Year = year,
                PaymentRate = paymentRate ?? 0m,
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
            if (lower.Contains("hcpcs") && (lower.Contains("description") || lower.Contains("payment") || lower.Contains("rate")))
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
