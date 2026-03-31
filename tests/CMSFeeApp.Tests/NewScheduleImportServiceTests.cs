using CMSFeeApp.Data;
using CMSFeeApp.Data.Repositories;
using CMSFeeApp.Data.Services;
using Xunit;

namespace CMSFeeApp.Tests;

public class ClfsImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;
    private readonly ClfsRepository _clfsRepo;
    private readonly ImportLogRepository _importLogRepo;
    private readonly ClfsImportService _service;

    public ClfsImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        _clfsRepo = new ClfsRepository(_context);
        _importLogRepo = new ImportLogRepository(_context);
        _service = new ClfsImportService(_clfsRepo, _importLogRepo);
    }

    [Fact]
    public void ParseCsv_ValidFile_ReturnsRecords()
    {
        var content = "HCPCS Code,Description,Payment Limit,Modifier\n" +
                      "86003,Allergen IgE allergy test,20.00,\n" +
                      "36415,Routine venipuncture,3.00,\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Equal(2, records.Count);
        Assert.Equal("86003", records[0].HcpcsCode);
        Assert.Equal(20.00m, records[0].PaymentLimit);
        Assert.Equal(2025, records[0].Year);
    }

    [Fact]
    public void ParseCsv_WithPreamble_SkipsPreambleAndParsesData()
    {
        var content = "CMS Clinical Laboratory Fee Schedule 2025\n" +
                      "Report generated: 2025-01-01\n\n" +
                      "HCPCS Code,Description,Payment Limit\n" +
                      "86003,Allergen IgE allergy test,20.00\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Single(records);
        Assert.Equal("86003", records[0].HcpcsCode);
        Assert.Equal(20.00m, records[0].PaymentLimit);
    }

    [Fact]
    public void ParseCsv_EmptyHcpcs_SkipsRecord()
    {
        var content = "HCPCS Code,Description,Payment Limit\n" +
                      ",Empty code,5.00\n" +
                      "86003,Allergen test,20.00\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Single(records);
        Assert.Equal("86003", records[0].HcpcsCode);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidFile_InsertsRecordsAndLogs()
    {
        var content = "HCPCS Code,Description,Payment Limit\n" +
                      "86003,Allergen IgE allergy test,20.00\n";
        var path = WriteTempFile(content);

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsImported);

        var fees = _clfsRepo.GetFees(2025);
        Assert.Single(fees);
        Assert.Equal("86003", fees[0].HcpcsCode);
        Assert.Equal(20.00m, fees[0].PaymentLimit);
    }

    [Fact]
    public async Task ImportFromFileAsync_EmptyFile_ReturnsFailed()
    {
        var path = WriteTempFile("");

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsImported);
    }

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

public class AspImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;
    private readonly AspRepository _aspRepo;
    private readonly ImportLogRepository _importLogRepo;
    private readonly AspImportService _service;

    public AspImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        _aspRepo = new AspRepository(_context);
        _importLogRepo = new ImportLogRepository(_context);
        _service = new AspImportService(_aspRepo, _importLogRepo);
    }

    [Fact]
    public void ParseCsv_ValidFile_ReturnsRecords()
    {
        var content = "HCPCS Code,Description,Payment Limit,Dosage Descriptor\n" +
                      "J0120,Tetracycline injection,1.50,up to 250 mg\n" +
                      "J0130,Abciximab injection,392.43,10 mg\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Equal(2, records.Count);
        Assert.Equal("J0120", records[0].HcpcsCode);
        Assert.Equal(1.50m, records[0].PaymentLimit);
        Assert.Equal("up to 250 mg", records[0].DosageDescriptor);
        Assert.Equal(2025, records[0].Year);
        Assert.Equal(1, records[0].Quarter); // default quarter
    }

    [Fact]
    public void ParseCsv_WithQuarterColumn_UsesQuarterValue()
    {
        var content = "HCPCS Code,Description,Payment Limit,Quarter\n" +
                      "J0120,Tetracycline injection,1.50,3\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Single(records);
        Assert.Equal(3, records[0].Quarter);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidFile_InsertsRecordsAndLogs()
    {
        var content = "HCPCS Code,Description,Payment Limit\n" +
                      "J0120,Tetracycline injection,1.50\n";
        var path = WriteTempFile(content);

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsImported);

        var fees = _aspRepo.GetFees(2025);
        Assert.Single(fees);
        Assert.Equal("J0120", fees[0].HcpcsCode);
        Assert.Equal(1.50m, fees[0].PaymentLimit);
    }

    [Fact]
    public async Task ImportFromFileAsync_EmptyFile_ReturnsFailed()
    {
        var path = WriteTempFile("");

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsImported);
    }

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

public class OppsImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;
    private readonly OppsRepository _oppsRepo;
    private readonly ImportLogRepository _importLogRepo;
    private readonly OppsImportService _service;

    public OppsImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        _oppsRepo = new OppsRepository(_context);
        _importLogRepo = new ImportLogRepository(_context);
        _service = new OppsImportService(_oppsRepo, _importLogRepo);
    }

    [Fact]
    public void ParseCsv_ValidFile_ReturnsRecords()
    {
        var content = "HCPCS Code,Description,APC Code,Payment Rate,Status Indicator\n" +
                      "99213,Office visit,5012,83.41,A\n" +
                      "70553,MRI brain,8005,1234.56,S\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Equal(2, records.Count);
        Assert.Equal("99213", records[0].HcpcsCode);
        Assert.Equal("5012", records[0].ApcCode);
        Assert.Equal(83.41m, records[0].PaymentRate);
        Assert.Equal("A", records[0].StatusIndicator);
        Assert.Equal(2025, records[0].Year);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidFile_InsertsRecordsAndLogs()
    {
        var content = "HCPCS Code,Description,APC Code,Payment Rate,Status Indicator\n" +
                      "99213,Office visit,5012,83.41,A\n";
        var path = WriteTempFile(content);

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsImported);

        var fees = _oppsRepo.GetFees(2025);
        Assert.Single(fees);
        Assert.Equal("99213", fees[0].HcpcsCode);
        Assert.Equal(83.41m, fees[0].PaymentRate);
        Assert.Equal("5012", fees[0].ApcCode);
    }

    [Fact]
    public async Task ImportFromFileAsync_EmptyFile_ReturnsFailed()
    {
        var path = WriteTempFile("");

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsImported);
    }

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

public class AscImportServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly DatabaseContext _context;
    private readonly AscRepository _ascRepo;
    private readonly ImportLogRepository _importLogRepo;
    private readonly AscImportService _service;

    public AscImportServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _context = new DatabaseContext(_tempDbPath);
        var runner = new MigrationRunner(_context);
        runner.RunMigrations();

        _ascRepo = new AscRepository(_context);
        _importLogRepo = new ImportLogRepository(_context);
        _service = new AscImportService(_ascRepo, _importLogRepo);
    }

    [Fact]
    public void ParseCsv_ValidFile_ReturnsRecords()
    {
        var content = "HCPCS Code,Description,Payment Rate\n" +
                      "19301,Breast lumpectomy,1012.34\n" +
                      "27447,Total knee arthroplasty,5678.90\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Equal(2, records.Count);
        Assert.Equal("19301", records[0].HcpcsCode);
        Assert.Equal(1012.34m, records[0].PaymentRate);
        Assert.Equal(2025, records[0].Year);
    }

    [Fact]
    public void ParseCsv_WithPreamble_SkipsPreamble()
    {
        var content = "CMS ASC Fee Schedule 2025\n\n" +
                      "HCPCS Code,Description,Payment Rate\n" +
                      "19301,Breast lumpectomy,1012.34\n";
        var path = WriteTempFile(content);

        var records = _service.ParseCsv(path, 2025);

        Assert.Single(records);
        Assert.Equal("19301", records[0].HcpcsCode);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidFile_InsertsRecordsAndLogs()
    {
        var content = "HCPCS Code,Description,Payment Rate\n" +
                      "19301,Breast lumpectomy,1012.34\n";
        var path = WriteTempFile(content);

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsImported);

        var fees = _ascRepo.GetFees(2025);
        Assert.Single(fees);
        Assert.Equal("19301", fees[0].HcpcsCode);
        Assert.Equal(1012.34m, fees[0].PaymentRate);
    }

    [Fact]
    public async Task ImportFromFileAsync_EmptyFile_ReturnsFailed()
    {
        var path = WriteTempFile("");

        var result = await _service.ImportFromFileAsync(path, 2025);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsImported);
    }

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
