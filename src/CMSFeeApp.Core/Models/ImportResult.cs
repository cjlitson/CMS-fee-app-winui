namespace CMSFeeApp.Core.Models;

public class ImportResult
{
    public bool Success { get; set; }
    public int RecordsImported { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
