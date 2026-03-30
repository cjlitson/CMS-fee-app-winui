using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public enum ExportFormat { Csv, Xlsx, Pdf }

public interface IFeeExporter
{
    FeeScheduleType ScheduleType { get; }
    IReadOnlyList<ExportFormat> SupportedFormats { get; }

    Task ExportAsync(ExportFormat format, string outputPath, int year, string? stateAbbr, CancellationToken ct = default);
}
