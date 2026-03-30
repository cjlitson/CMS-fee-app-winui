using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public interface IFeeImporter
{
    FeeScheduleType ScheduleType { get; }

    /// <summary>Supported file extensions (e.g., ".csv", ".txt", ".xlsx").</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<ImportResult> ImportFromFileAsync(string filePath, int year, CancellationToken ct = default);
}
