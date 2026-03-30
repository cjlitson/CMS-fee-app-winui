using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
