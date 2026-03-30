using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public interface IDmeposService
{
    Task<IReadOnlyList<DmepsFee>> GetFeesAsync(int year, string? stateAbbr = null, string? hcpcsCode = null, string? descriptionKeyword = null, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromFileAsync(string filePath, int year, CancellationToken cancellationToken = default);
    Task<ImportResult> SyncFromCmsAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);
}
