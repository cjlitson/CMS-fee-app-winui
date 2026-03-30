using System.Globalization;
using System.Text.RegularExpressions;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Parses CMS DMEPOS fee schedule files (tilde-delimited TXT and grid-format CSV)
/// and imports records into the database.
/// Ported from the Python app's core/importer.py.
/// </summary>
public class DmeposImportService : IFeeImporter
{
    private readonly DmeposRepository _repository;
    private readonly ImportLogRepository _importLogRepository;

    private static readonly IReadOnlyList<string> _supportedExtensions = [".csv", ".txt"];

    // CMS tilde-delimited TXT column positions (0-based)
    private const int TxtYear = 0;
    private const int TxtHcpcs = 1;
    private const int TxtMod1 = 2;
    private const int TxtMod2 = 3;
    private const int TxtState = 8;
    private const int TxtNrUpdated = 11;
    private const int TxtRUpdated = 12;
    private const int TxtRuralInd = 13;
    private const int TxtDesc = 16;

    // State column header regex: "AZ (NR)", "AZ(R)", "AZ NR", "AZ-R", etc.
    private static readonly Regex StateColRegex = new(
        @"^([A-Z]{2})\s*(?:\((?<kind1>NR|R)\)|\[(?<kind2>NR|R)\]|[-\s](?<kind3>NR|R))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FeeScheduleType ScheduleType => FeeScheduleType.Dmepos;
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

    public DmeposImportService(DmeposRepository repository, ImportLogRepository importLogRepository)
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
                _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, filePath, 0, false, msg);
                return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = msg };
            }

            await Task.Run(() => _repository.InsertFees(fees), ct);

            _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, filePath, fees.Count, true);
            return new ImportResult { Success = true, RecordsImported = fees.Count };
        }
        catch (Exception ex)
        {
            _importLogRepository.LogImport(FeeScheduleType.Dmepos, year, filePath, 0, false, ex.Message);
            return new ImportResult { Success = false, RecordsImported = 0, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Parses a DMEPOS fee file. Dispatches to tilde-delimited or CSV parser
    /// based on delimiter detection.
    /// </summary>
    internal IReadOnlyList<DmepsFee> ParseFile(string filePath, int year)
    {
        var delimiter = DetectDelimiter(filePath);
        if (delimiter == '~')
            return ParseTildeTxt(filePath, year);
        return ParseGridCsv(filePath, year, delimiter);
    }

    /// <summary>
    /// Detect whether a file uses '~', '|', or ',' as delimiter.
    /// Scans up to the first 30 non-empty lines.
    /// </summary>
    public static char DetectDelimiter(string filePath)
    {
        try
        {
            using var f = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
            int linesChecked = 0;
            string? line;
            while ((line = f.ReadLine()) != null && linesChecked < 30)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                int tildes = line.Count(c => c == '~');
                int pipes = line.Count(c => c == '|');
                int commas = line.Count(c => c == ',');

                if (tildes >= 3 && tildes >= pipes && tildes >= commas)
                    return '~';
                if (pipes >= 3 && pipes > commas)
                    return '|';
                if (commas >= 3)
                    return ',';

                linesChecked++;
            }
        }
        catch { }
        return ',';
    }

    /// <summary>
    /// Parse a CMS DMEPOS tilde-delimited TXT file (no header row).
    /// </summary>
    public IReadOnlyList<DmepsFee> ParseTildeTxt(string filePath, int year)
    {
        var records = new List<DmepsFee>();
        var importedAt = DateTime.UtcNow;

        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split('~').Select(p => p.Trim()).ToArray();
            if (parts.Length < 12) continue;

            int recYear = year;
            if (recYear == 0)
            {
                if (!int.TryParse(parts[TxtYear], out recYear))
                    continue;
            }

            var hcpcs = parts.Length > TxtHcpcs ? parts[TxtHcpcs].ToUpperInvariant().Trim() : "";
            if (string.IsNullOrEmpty(hcpcs)) continue;

            var state = parts.Length > TxtState ? parts[TxtState].ToUpperInvariant().Trim() : "";
            if (string.IsNullOrEmpty(state)) continue;

            var mod1 = parts.Length > TxtMod1 ? parts[TxtMod1].Trim() : "";
            var mod2 = parts.Length > TxtMod2 ? parts[TxtMod2].Trim() : "";
            string? modifier = BuildModifier(mod1, mod2);

            var nrRaw = parts.Length > TxtNrUpdated ? parts[TxtNrUpdated] : "";
            var rRaw = parts.Length > TxtRUpdated ? parts[TxtRUpdated] : "";
            var ruralInd = parts.Length > TxtRuralInd ? parts[TxtRuralInd].Trim() : "0";

            var allowableNr = ParseAmount(nrRaw);
            var allowableRRaw = ParseAmount(rRaw);
            var allowableR = ruralInd == "1" && allowableRRaw.HasValue ? allowableRRaw : null;

            var description = parts.Length > TxtDesc ? parts[TxtDesc].Trim() : "";

            records.Add(new DmepsFee
            {
                HcpcsCode = hcpcs,
                Description = description,
                StateAbbr = state,
                Year = recYear,
                Allowable = allowableNr ?? 0m,
                AllowableNr = allowableNr,
                AllowableR = allowableR,
                Modifier = modifier,
                DataSource = "file_import",
                ImportedAt = importedAt
            });
        }

        return records;
    }

    /// <summary>
    /// Parse a CMS DMEPOS grid-format CSV (one row per HCPCS, states as columns).
    /// Skips preamble rows before the real header line.
    /// </summary>
    public IReadOnlyList<DmepsFee> ParseGridCsv(string filePath, int year, char delimiter = ',')
    {
        var records = new List<DmepsFee>();
        var importedAt = DateTime.UtcNow;

        // First pass: find header line index
        int headerLineIdx;
        using (var scan = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
        {
            (headerLineIdx, _) = FindCsvHeaderLine(scan);
        }
        if (headerLineIdx < 0) return records;

        // Second pass: open fresh and skip to the header
        using var stream = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        for (int i = 0; i < headerLineIdx; i++)
            stream.ReadLine();

        var headerRow = stream.ReadLine();
        if (headerRow == null) return records;
        var headers = ParseCsvLine(headerRow, delimiter)
            .Select(h => h.Trim())
            .ToArray();

        string? line;
        while ((line = stream.ReadLine()) != null)
        {
            var values = ParseCsvLine(line, delimiter);
            if (values.Length == 0 || values.All(v => string.IsNullOrWhiteSpace(v)))
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
                row[headers[i]] = (values[i] ?? "").Trim();

            var hcpcs = (GetColumnValue(row, "hcpcs", "hcpcs_cd", "hcpcs code", "hcpcs_code") ?? "").ToUpperInvariant().Trim();
            if (string.IsNullOrEmpty(hcpcs)) continue;

            var description = GetColumnValue(row, "description", "long_description") ?? "";
            var mod1Raw = (GetColumnValue(row, "mod", "modifier") ?? "").Trim();
            var mod2Raw = (GetColumnValue(row, "mod2") ?? "").Trim();
            string? modifier = BuildModifier(mod1Raw, mod2Raw);

            var nrByState = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var rByState = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                var m = StateColRegex.Match(header.Trim());
                if (!m.Success) continue;

                var st = m.Groups[1].Value.ToUpperInvariant();
                var kind = (m.Groups["kind1"].Value
                    + m.Groups["kind2"].Value
                    + m.Groups["kind3"].Value).ToUpperInvariant();
                var amount = ParseAmount(row.TryGetValue(header, out var val) ? val : "");

                if (kind == "NR")
                    nrByState[st] = amount;
                else
                    rByState[st] = amount;
            }

            var statesToEmit = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var st in nrByState.Keys) statesToEmit.Add(st);
            foreach (var st in rByState.Keys) statesToEmit.Add(st);

            foreach (var st in statesToEmit)
            {
                var allowableNr = nrByState.TryGetValue(st, out var nr) ? nr : null;
                var allowableR = rByState.TryGetValue(st, out var r) ? r : null;

                records.Add(new DmepsFee
                {
                    HcpcsCode = hcpcs,
                    Description = description,
                    StateAbbr = st.ToUpperInvariant(),
                    Year = year,
                    Allowable = allowableNr ?? 0m,
                    AllowableNr = allowableNr,
                    AllowableR = allowableR,
                    Modifier = modifier,
                    DataSource = "file_import",
                    ImportedAt = importedAt
                });
            }
        }

        return records;
    }

    private static (int index, string? line) FindCsvHeaderLine(StreamReader reader)
    {
        int idx = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("hcpcs") && lower.Contains("description"))
                return (idx, line);
            idx++;
        }
        return (-1, null);
    }

    public static string[] ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i += 2; }
                        else { i++; break; }
                    }
                    else { sb.Append(line[i++]); }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == delimiter) i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && line[i] != delimiter) i++;
                fields.Add(line[start..i]);
                if (i < line.Length && line[i] == delimiter) i++;
            }
        }
        // Handle trailing delimiter → implicit empty final field
        if (line.Length > 0 && line[^1] == delimiter)
            fields.Add("");
        return fields.ToArray();
    }

    private static string? GetColumnValue(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
            if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        return null;
    }

    public static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Replace("$", "").Replace(",", "").Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            return val != 0m ? val : null;
        return null;
    }

    private static string? BuildModifier(string mod1, string mod2)
    {
        if (!string.IsNullOrEmpty(mod1) && !string.IsNullOrEmpty(mod2))
            return $"{mod1},{mod2}";
        if (!string.IsNullOrEmpty(mod1)) return mod1;
        if (!string.IsNullOrEmpty(mod2)) return mod2;
        return null;
    }
}
