using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public interface ICmsSyncService
{
    FeeScheduleType ScheduleType { get; }

    Task<ImportResult> SyncFromCmsAsync(int year, IProgress<string>? progress = null, CancellationToken ct = default);
}
