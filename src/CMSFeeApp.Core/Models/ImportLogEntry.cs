namespace CMSFeeApp.Core.Models;

public class ImportLogEntry
{
    public int Id { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? FilePath { get; set; }
    public int RecordsImported { get; set; }
    public DateTime ImportedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
