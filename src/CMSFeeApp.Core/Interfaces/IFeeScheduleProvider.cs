using CMSFeeApp.Core.Models;

namespace CMSFeeApp.Core.Interfaces;

/// <summary>
/// Implemented by each fee schedule type. Provides metadata and capabilities.
/// </summary>
public interface IFeeScheduleProvider
{
    FeeScheduleType ScheduleType { get; }
    string DisplayName { get; }
    bool SupportsStateFilter { get; }
    bool SupportsFacilityNonFacility { get; }

    /// <summary>Column definitions for the data grid (so the UI can adapt dynamically).</summary>
    IReadOnlyList<FeeColumnDefinition> GetColumnDefinitions();
}
