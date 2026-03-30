using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Core.Services;
using CMSFeeApp.Data.Repositories;
using CMSFeeApp.Data.Services;
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
    private readonly DmeposImportService _dmeposImportService;
    private readonly PfsImportService _pfsImportService;
    private readonly FeeExportService _dmeposExportService;
    private readonly FeeExportService _pfsExportService;
    private readonly DmeposCmsSyncService _dmeposSyncService;
    private readonly PfsCmsSyncService _pfsSyncService;

    private CancellationTokenSource? _syncCts;

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

    // Window handle used for file pickers
    public Microsoft.UI.Xaml.Window? Window { get; set; }

    public MainViewModel(
        UpdateService updateService,
        DmeposRepository dmeposRepository,
        PfsRepository pfsRepository,
        DmeposImportService dmeposImportService,
        PfsImportService pfsImportService,
        FeeExportService dmeposExportService,
        FeeExportService pfsExportService,
        DmeposCmsSyncService dmeposSyncService,
        PfsCmsSyncService pfsSyncService)
    {
        _updateService = updateService;
        _dmeposRepository = dmeposRepository;
        _pfsRepository = pfsRepository;
        _dmeposImportService = dmeposImportService;
        _pfsImportService = pfsImportService;
        _dmeposExportService = dmeposExportService;
        _pfsExportService = pfsExportService;
        _dmeposSyncService = dmeposSyncService;
        _pfsSyncService = pfsSyncService;

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
        if (IsLoading) return;

        _syncCts?.Cancel();
        _syncCts = new CancellationTokenSource();
        var ct = _syncCts.Token;

        IsLoading = true;
        var progress = new Progress<string>(msg => StatusMessage = msg);

        try
        {
            ImportResult result;
            if (SelectedScheduleType == ScheduleType.Dmepos)
                result = await _dmeposSyncService.SyncFromCmsAsync(SelectedYear, progress, ct);
            else
                result = await _pfsSyncService.SyncFromCmsAsync(SelectedYear, progress, ct);

            if (result.Success)
            {
                StatusMessage = $"Sync complete: {result.RecordsImported:N0} records imported for {SelectedYear}.";
                await LoadAvailableStatesAsync();
                await SearchAsync();
            }
            else
            {
                StatusMessage = $"Sync failed: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsLoading) return;

        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, GetWindowHandle());
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            if (SelectedScheduleType == ScheduleType.Dmepos)
            {
                picker.FileTypeFilter.Add(".csv");
                picker.FileTypeFilter.Add(".txt");
            }
            else
            {
                picker.FileTypeFilter.Add(".csv");
            }

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            IsLoading = true;
            StatusMessage = $"Importing {file.Name}…";

            ImportResult result;
            var ct = CancellationToken.None;
            if (SelectedScheduleType == ScheduleType.Dmepos)
                result = await _dmeposImportService.ImportFromFileAsync(file.Path, SelectedYear, ct);
            else
                result = await _pfsImportService.ImportFromFileAsync(file.Path, SelectedYear, ct);

            if (result.Success)
            {
                StatusMessage = $"Import complete: {result.RecordsImported:N0} records imported.";
                await LoadAvailableStatesAsync();
                await SearchAsync();
            }
            else
            {
                StatusMessage = $"Import failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsLoading) return;

        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, GetWindowHandle());
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            picker.FileTypeChoices.Add("CSV file", [".csv"]);
            picker.FileTypeChoices.Add("Excel workbook", [".xlsx"]);
            picker.FileTypeChoices.Add("PDF document", [".pdf"]);

            var scheduleLabel = SelectedScheduleType == ScheduleType.Dmepos ? "DMEPOS" : "PFS";
            picker.SuggestedFileName = $"{scheduleLabel}_FeeSchedule_{SelectedYear}";

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            IsLoading = true;
            StatusMessage = "Exporting…";

            var format = file.FileType?.ToLowerInvariant() switch
            {
                ".xlsx" => CMSFeeApp.Core.Interfaces.ExportFormat.Xlsx,
                ".pdf" => CMSFeeApp.Core.Interfaces.ExportFormat.Pdf,
                _ => CMSFeeApp.Core.Interfaces.ExportFormat.Csv
            };

            var exporter = SelectedScheduleType == ScheduleType.Dmepos
                ? _dmeposExportService
                : _pfsExportService;

            await exporter.ExportAsync(format, file.Path, SelectedYear, SelectedState);
            StatusMessage = $"Export complete: {file.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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

        await LoadAvailableStatesAsync();
    }

    private async Task LoadAvailableStatesAsync()
    {
        try
        {
            if (SelectedScheduleType == ScheduleType.Dmepos)
            {
                var states = await Task.Run(() => _dmeposRepository.GetAvailableStates());
                AvailableStates.Clear();
                foreach (var s in states)
                    AvailableStates.Add(s);
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // DB not yet populated
        }
    }

    private IntPtr GetWindowHandle()
    {
        if (Window is not null)
            return WinRT.Interop.WindowNative.GetWindowHandle(Window);
        return IntPtr.Zero;
    }
}
