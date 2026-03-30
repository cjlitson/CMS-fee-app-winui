using CMSFeeApp.Data;
using CMSFeeApp.Data.Repositories;
using CMSFeeApp.Data.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CMSFeeApp.Tests;

public class DmeposImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;
    private readonly DmeposRepository _dmeposRepo;
    private readonly ImportLogRepository _importLogRepo;
    private readonly DmeposImportService _service;

    public DmeposImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        _dmeposRepo = new DmeposRepository(_context);
        _importLogRepo = new ImportLogRepository(_context);
        _service = new DmeposImportService(_dmeposRepo, _importLogRepo);
    }

    // -------------------------------------------------------------------------
    // Delimiter detection tests
    // -------------------------------------------------------------------------

    [Fact]
    public void DetectDelimiter_TildeFile_ReturnsTilde()
    {
        var path = WriteTempFile("2025~A4216~  ~  ~J~OS~A~00~AZ     ~000000.35~000000.62~000000.53~000000.61~0~1~ ~Sterile water");
        Assert.Equal('~', DmeposImportService.DetectDelimiter(path));
    }

    [Fact]
    public void DetectDelimiter_CsvFile_ReturnsComma()
    {
        var path = WriteTempFile("HCPCS Code,Description,AZ (NR),AZ (R)\nA4216,Sterile water,0.35,0.61");
        Assert.Equal(',', DmeposImportService.DetectDelimiter(path));
    }

    [Fact]
    public void DetectDelimiter_PipeFile_ReturnsPipe()
    {
        var path = WriteTempFile("HCPCS|Description|AZ NR|AZ R\nA4216|Sterile water|0.35|0.61");
        Assert.Equal('|', DmeposImportService.DetectDelimiter(path));
    }

    // -------------------------------------------------------------------------
    // Tilde-delimited TXT parser tests
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseTildeTxt_ValidLine_ReturnsSingleRecord()
    {
        // Format: year~hcpcs~mod1~mod2~juris~catg~pricing~multi~state~floor~ceiling~nr_updated~r_updated~rural_ind~...~desc
        var line = "2025~A4216~  ~  ~J~OS~A~00~AZ~000000.35~000000.62~000000.53~000000.61~1~1~ ~Sterile water/saline, 10 ml";
        var path = WriteTempFile(line);

        var records = _service.ParseTildeTxt(path, 2025);

        Assert.Single(records);
        var rec = records[0];
        Assert.Equal("A4216", rec.HcpcsCode);
        Assert.Equal("AZ", rec.StateAbbr);
        Assert.Equal(2025, rec.Year);
        Assert.Equal(0.53m, rec.AllowableNr);
        Assert.Equal(0.61m, rec.AllowableR);
        Assert.Equal("Sterile water/saline, 10 ml", rec.Description);
    }

    [Fact]
    public void ParseTildeTxt_RuralIndicatorZero_NullAllowableR()
    {
        var line = "2025~A4216~  ~  ~J~OS~A~00~AZ~000000.35~000000.62~000000.53~000000.61~0~1~ ~Sterile water";
        var path = WriteTempFile(line);

        var records = _service.ParseTildeTxt(path, 2025);

        Assert.Single(records);
        Assert.Null(records[0].AllowableR);
        Assert.Equal(0.53m, records[0].AllowableNr);
    }

    [Fact]
    public void ParseTildeTxt_EmptyState_SkipsRecord()
    {
        var line = "2025~A4216~  ~  ~J~OS~A~00~   ~000000.53~000000.62~000000.53~000000.61~1~1~ ~Desc";
        var path = WriteTempFile(line);

        var records = _service.ParseTildeTxt(path, 2025);

        Assert.Empty(records);
    }

    [Fact]
    public void ParseTildeTxt_TooFewColumns_SkipsRecord()
    {
        var line = "2025~A4216~  ~  ~J~OS";
        var path = WriteTempFile(line);

        var records = _service.ParseTildeTxt(path, 2025);

        Assert.Empty(records);
    }

    // -------------------------------------------------------------------------
    // CSV grid parser tests
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseGridCsv_WithPreamble_SkipsPreambleAndParsesData()
    {
        var content = """
            CMS DMEPOS Fee Schedule 2025
            Report generated: 2025-01-01
            
            HCPCS,Description,AZ (NR),AZ (R),TX (NR),TX (R)
            A4216,Sterile water,0.53,0.61,0.48,0.55
            E0100,Cane,12.50,,11.75,
            """;
        var path = WriteTempFile(content);

        var records = _service.ParseGridCsv(path, 2025);

        // 2 HCPCS × 2 states (AZ and TX) = 4 records
        Assert.Equal(4, records.Count);

        var azRecord = records.First(r => r.HcpcsCode == "A4216" && r.StateAbbr == "AZ");
        Assert.Equal(0.53m, azRecord.AllowableNr);
        Assert.Equal(0.61m, azRecord.AllowableR);

        var txRecord = records.First(r => r.HcpcsCode == "E0100" && r.StateAbbr == "TX");
        Assert.Equal(11.75m, txRecord.AllowableNr);
        Assert.Null(txRecord.AllowableR);
    }

    [Fact]
    public void ParseGridCsv_NoPreamble_ParsesData()
    {
        var content = "HCPCS,Description,CA (NR),CA (R)\nA0001,Ambulance,50.00,55.00\n";
        var path = WriteTempFile(content);

        var records = _service.ParseGridCsv(path, 2025);

        Assert.Single(records);
        Assert.Equal("A0001", records[0].HcpcsCode);
        Assert.Equal("CA", records[0].StateAbbr);
        Assert.Equal(50.00m, records[0].AllowableNr);
        Assert.Equal(55.00m, records[0].AllowableR);
    }

    [Fact]
    public void ParseGridCsv_NoHeaderLine_ReturnsEmpty()
    {
        var content = "just some data without a header\n1,2,3,4\n";
        var path = WriteTempFile(content);

        var records = _service.ParseGridCsv(path, 2025);

        Assert.Empty(records);
    }

    // -------------------------------------------------------------------------
    // CSV parsing helper tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("a,b,c", ',', new[] { "a", "b", "c" })]
    [InlineData("\"a,b\",c", ',', new[] { "a,b", "c" })]
    [InlineData("\"a\"\"b\",c", ',', new[] { "a\"b", "c" })]
    [InlineData("a~b~c", '~', new[] { "a", "b", "c" })]
    public void ParseCsvLine_ReturnsExpectedFields(string line, char delimiter, string[] expected)
    {
        var result = DmeposImportService.ParseCsvLine(line, delimiter);
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // Import integration test (DB round-trip)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportFromFileAsync_ValidTildeFile_InsertsRecordsAndLogs()
    {
        var line = "2025~A4216~  ~  ~J~OS~A~00~AZ~000000.35~000000.62~000000.53~000000.61~1~1~ ~Sterile water";
        var path = WriteTempFile(line);

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsImported);

        var fees = _dmeposRepo.GetFees(2025, "AZ");
        Assert.Single(fees);
        Assert.Equal("A4216", fees[0].HcpcsCode);
        Assert.Equal(0.53m, fees[0].AllowableNr);
        Assert.Equal(0.61m, fees[0].AllowableR);

        var logs = _importLogRepo.GetRecentLogs(10);
        Assert.True(logs.Count > 0);
        Assert.True(logs[0].Success);
        Assert.Equal(1, logs[0].RecordsImported);
    }

    [Fact]
    public async Task ImportFromFileAsync_EmptyFile_ReturnsFailed()
    {
        var path = WriteTempFile("");

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsImported);
        Assert.NotNull(result.ErrorMessage);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        return path;
    }

    public void Dispose()
    {
        _context.Dispose();
        if (File.Exists(_tempDbPath))
            File.Delete(_tempDbPath);
    }
}
