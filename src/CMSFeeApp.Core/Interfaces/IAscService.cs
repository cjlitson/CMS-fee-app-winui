using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public interface IAscService
{
    Task<IReadOnlyList<AscFee>> GetFeesAsync(int year, string? hcpcsCode = null, string? descriptionKeyword = null, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromFileAsync(string filePath, int year, CancellationToken cancellationToken = default);
    Task<ImportResult> SyncFromCmsAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);
}
