using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Core.Services;
using CMSFeeApp.Data.Repositories;
using System.Collections.ObjectModel;

namespace CMSFeeApp.WinUI.ViewModels;

public enum ScheduleType
{
    Dmepos,
    PfsNational
}

public partial class MainViewModel : ObservableObject
{
    private readonly UpdateService _updateService;
    private readonly DmeposRepository _dmeposRepository;
    private readonly PfsRepository _pfsRepository;

    [ObservableProperty]
    private ScheduleType _selectedScheduleType = ScheduleType.Dmepos;

    [ObservableProperty]
    private int _selectedYear = DateTime.Now.Year;

    [ObservableProperty]
    private string? _selectedState;

    [ObservableProperty]
    private string _codeFilter = string.Empty;

    [ObservableProperty]
    private string _descriptionFilter = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string? _updateVersion;

    [ObservableProperty]
    private string? _updateUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPfsVisible))]
    private bool _isStateVisible = true;

    public bool IsPfsVisible => !IsStateVisible;

    public ObservableCollection<int> AvailableYears { get; } = new();
    public ObservableCollection<string> AvailableStates { get; } = new();
    public ObservableCollection<DmepsFee> DmeposResults { get; } = new();
    public ObservableCollection<PfsFee> PfsResults { get; } = new();

    public MainViewModel(UpdateService updateService, DmeposRepository dmeposRepository, PfsRepository pfsRepository)
    {
        _updateService = updateService;
        _dmeposRepository = dmeposRepository;
        _pfsRepository = pfsRepository;

        // Initialize with current and recent years
        for (var y = DateTime.Now.Year; y >= 2020; y--)
            AvailableYears.Add(y);

        _ = CheckForUpdateAsync();
        _ = LoadAvailableYearsAsync();
    }

    partial void OnSelectedScheduleTypeChanged(ScheduleType value)
    {
        IsStateVisible = value == ScheduleType.Dmepos;
        _ = SearchAsync();
    }

    partial void OnSelectedYearChanged(int value) => _ = SearchAsync();
    partial void OnSelectedStateChanged(string? value) => _ = SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusMessage = "Searching...";

        try
        {
            if (SelectedScheduleType == ScheduleType.Dmepos)
            {
                var fees = await Task.Run(() =>
                    _dmeposRepository.GetFees(SelectedYear, SelectedState, CodeFilter, DescriptionFilter));
                DmeposResults.Clear();
                foreach (var fee in fees)
                    DmeposResults.Add(fee);
                StatusMessage = $"{DmeposResults.Count} records found";
            }
            else
            {
                var fees = await Task.Run(() =>
                    _pfsRepository.GetFees(SelectedYear, CodeFilter, DescriptionFilter));
                PfsResults.Clear();
                foreach (var fee in fees)
                    PfsResults.Add(fee);
                StatusMessage = $"{PfsResults.Count} records found";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        StatusMessage = "Sync not yet implemented – coming in Phase 2";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        StatusMessage = "Import not yet implemented – coming in Phase 2";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        StatusMessage = "Export not yet implemented – coming in Phase 2";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenUpdateUrl()
    {
        if (UpdateUrl is not null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
            }
            catch
            {
                StatusMessage = "Could not open release page. Please visit GitHub manually.";
            }
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var info = await _updateService.CheckForUpdateAsync();
            IsUpdateAvailable = info.IsUpdateAvailable;
            UpdateVersion = info.LatestVersion;
            UpdateUrl = info.ReleaseUrl;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Update check is best-effort; network failures are expected in offline environments
        }
    }

    private async Task LoadAvailableYearsAsync()
    {
        try
        {
            IReadOnlyList<int> years;
            if (SelectedScheduleType == ScheduleType.Dmepos)
                years = await Task.Run(() => _dmeposRepository.GetAvailableYears());
            else
                years = await Task.Run(() => _pfsRepository.GetAvailableYears());

            if (years.Count > 0)
            {
                AvailableYears.Clear();
                foreach (var y in years)
                    AvailableYears.Add(y);
                if (!AvailableYears.Contains(SelectedYear))
                    SelectedYear = AvailableYears[0];
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // DB not yet populated; fall back to the default year list seeded in the constructor
        }
    }
}
