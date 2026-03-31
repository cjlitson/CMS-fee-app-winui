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
    PfsNational,
    ClinicalLab,
    AspDrug,
    Opps,
    Asc
}

public partial class MainViewModel : ObservableObject
{
    private readonly UpdateService _updateService;
    private readonly DmeposRepository _dmeposRepository;
    private readonly PfsRepository _pfsRepository;
    private readonly ClfsRepository _clfsRepository;
    private readonly AspRepository _aspRepository;
    private readonly OppsRepository _oppsRepository;
    private readonly AscRepository _ascRepository;
    private readonly DmeposImportService _dmeposImportService;
    private readonly PfsImportService _pfsImportService;
    private readonly ClfsImportService _clfsImportService;
    private readonly AspImportService _aspImportService;
    private readonly OppsImportService _oppsImportService;
    private readonly AscImportService _ascImportService;
    private readonly FeeExportService _dmeposExportService;
    private readonly FeeExportService _pfsExportService;
    private readonly FeeExportService _clfsExportService;
    private readonly FeeExportService _aspExportService;
    private readonly FeeExportService _oppsExportService;
    private readonly FeeExportService _ascExportService;
    private readonly DmeposCmsSyncService _dmeposSyncService;
    private readonly PfsCmsSyncService _pfsSyncService;
    private readonly ClfsCmsSyncService _clfsSyncService;
    private readonly AspCmsSyncService _aspSyncService;
    private readonly OppsCmsSyncService _oppsSyncService;
    private readonly AscCmsSyncService _ascSyncService;

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
    [NotifyPropertyChangedFor(nameof(IsClfsVisible))]
    [NotifyPropertyChangedFor(nameof(IsAspVisible))]
    [NotifyPropertyChangedFor(nameof(IsOppsVisible))]
    [NotifyPropertyChangedFor(nameof(IsAscVisible))]
    private bool _isStateVisible = true;

    public bool IsPfsVisible => SelectedScheduleType == ScheduleType.PfsNational;
    public bool IsClfsVisible => SelectedScheduleType == ScheduleType.ClinicalLab;
    public bool IsAspVisible => SelectedScheduleType == ScheduleType.AspDrug;
    public bool IsOppsVisible => SelectedScheduleType == ScheduleType.Opps;
    public bool IsAscVisible => SelectedScheduleType == ScheduleType.Asc;

    public ObservableCollection<int> AvailableYears { get; } = new();
    public ObservableCollection<string> AvailableStates { get; } = new();
    public ObservableCollection<DmepsFee> DmeposResults { get; } = new();
    public ObservableCollection<PfsFee> PfsResults { get; } = new();
    public ObservableCollection<ClfsFee> ClfsResults { get; } = new();
    public ObservableCollection<AspFee> AspResults { get; } = new();
    public ObservableCollection<OppsFee> OppsResults { get; } = new();
    public ObservableCollection<AscFee> AscResults { get; } = new();

    // Window handle used for file pickers
    public Microsoft.UI.Xaml.Window? Window { get; set; }

    public MainViewModel(
        UpdateService updateService,
        DmeposRepository dmeposRepository,
        PfsRepository pfsRepository,
        ClfsRepository clfsRepository,
        AspRepository aspRepository,
        OppsRepository oppsRepository,
        AscRepository ascRepository,
        DmeposImportService dmeposImportService,
        PfsImportService pfsImportService,
        ClfsImportService clfsImportService,
        AspImportService aspImportService,
        OppsImportService oppsImportService,
        AscImportService ascImportService,
        FeeExportService dmeposExportService,
        FeeExportService pfsExportService,
        FeeExportService clfsExportService,
        FeeExportService aspExportService,
        FeeExportService oppsExportService,
        FeeExportService ascExportService,
        DmeposCmsSyncService dmeposSyncService,
        PfsCmsSyncService pfsSyncService,
        ClfsCmsSyncService clfsSyncService,
        AspCmsSyncService aspSyncService,
        OppsCmsSyncService oppsSyncService,
        AscCmsSyncService ascSyncService)
    {
        _updateService = updateService;
        _dmeposRepository = dmeposRepository;
        _pfsRepository = pfsRepository;
        _clfsRepository = clfsRepository;
        _aspRepository = aspRepository;
        _oppsRepository = oppsRepository;
        _ascRepository = ascRepository;
        _dmeposImportService = dmeposImportService;
        _pfsImportService = pfsImportService;
        _clfsImportService = clfsImportService;
        _aspImportService = aspImportService;
        _oppsImportService = oppsImportService;
        _ascImportService = ascImportService;
        _dmeposExportService = dmeposExportService;
        _pfsExportService = pfsExportService;
        _clfsExportService = clfsExportService;
        _aspExportService = aspExportService;
        _oppsExportService = oppsExportService;
        _ascExportService = ascExportService;
        _dmeposSyncService = dmeposSyncService;
        _pfsSyncService = pfsSyncService;
        _clfsSyncService = clfsSyncService;
        _aspSyncService = aspSyncService;
        _oppsSyncService = oppsSyncService;
        _ascSyncService = ascSyncService;

        // Initialize with current and recent years
        for (var y = DateTime.Now.Year; y >= 2020; y--)
            AvailableYears.Add(y);

        _ = CheckForUpdateAsync();
        _ = LoadAvailableYearsAsync();
    }

    partial void OnSelectedScheduleTypeChanged(ScheduleType value)
    {
        IsStateVisible = value == ScheduleType.Dmepos;
        OnPropertyChanged(nameof(IsPfsVisible));
        OnPropertyChanged(nameof(IsClfsVisible));
        OnPropertyChanged(nameof(IsAspVisible));
        OnPropertyChanged(nameof(IsOppsVisible));
        OnPropertyChanged(nameof(IsAscVisible));
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
            switch (SelectedScheduleType)
            {
                case ScheduleType.Dmepos:
                {
                    var fees = await Task.Run(() =>
                        _dmeposRepository.GetFees(SelectedYear, SelectedState, CodeFilter, DescriptionFilter));
                    DmeposResults.Clear();
                    foreach (var fee in fees) DmeposResults.Add(fee);
                    StatusMessage = $"{DmeposResults.Count} records found";
                    break;
                }
                case ScheduleType.PfsNational:
                {
                    var fees = await Task.Run(() =>
                        _pfsRepository.GetFees(SelectedYear, CodeFilter, DescriptionFilter));
                    PfsResults.Clear();
                    foreach (var fee in fees) PfsResults.Add(fee);
                    StatusMessage = $"{PfsResults.Count} records found";
                    break;
                }
                case ScheduleType.ClinicalLab:
                {
                    var fees = await Task.Run(() =>
                        _clfsRepository.GetFees(SelectedYear, CodeFilter, DescriptionFilter));
                    ClfsResults.Clear();
                    foreach (var fee in fees) ClfsResults.Add(fee);
                    StatusMessage = $"{ClfsResults.Count} records found";
                    break;
                }
                case ScheduleType.AspDrug:
                {
                    var fees = await Task.Run(() =>
                        _aspRepository.GetFees(SelectedYear, hcpcsCode: CodeFilter, descriptionKeyword: DescriptionFilter));
                    AspResults.Clear();
                    foreach (var fee in fees) AspResults.Add(fee);
                    StatusMessage = $"{AspResults.Count} records found";
                    break;
                }
                case ScheduleType.Opps:
                {
                    var fees = await Task.Run(() =>
                        _oppsRepository.GetFees(SelectedYear, CodeFilter, DescriptionFilter));
                    OppsResults.Clear();
                    foreach (var fee in fees) OppsResults.Add(fee);
                    StatusMessage = $"{OppsResults.Count} records found";
                    break;
                }
                case ScheduleType.Asc:
                {
                    var fees = await Task.Run(() =>
                        _ascRepository.GetFees(SelectedYear, CodeFilter, DescriptionFilter));
                    AscResults.Clear();
                    foreach (var fee in fees) AscResults.Add(fee);
                    StatusMessage = $"{AscResults.Count} records found";
                    break;
                }
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
            ImportResult result = SelectedScheduleType switch
            {
                ScheduleType.Dmepos => await _dmeposSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                ScheduleType.PfsNational => await _pfsSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                ScheduleType.ClinicalLab => await _clfsSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                ScheduleType.AspDrug => await _aspSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                ScheduleType.Opps => await _oppsSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                ScheduleType.Asc => await _ascSyncService.SyncFromCmsAsync(SelectedYear, progress, ct),
                _ => await _dmeposSyncService.SyncFromCmsAsync(SelectedYear, progress, ct)
            };

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
            picker.FileTypeFilter.Add(".csv");
            picker.FileTypeFilter.Add(".txt");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            IsLoading = true;
            StatusMessage = $"Importing {file.Name}…";

            ImportResult result;
            var ct = CancellationToken.None;

            result = SelectedScheduleType switch
            {
                ScheduleType.Dmepos => await _dmeposImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                ScheduleType.PfsNational => await _pfsImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                ScheduleType.ClinicalLab => await _clfsImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                ScheduleType.AspDrug => await _aspImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                ScheduleType.Opps => await _oppsImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                ScheduleType.Asc => await _ascImportService.ImportFromFileAsync(file.Path, SelectedYear, ct),
                _ => await _dmeposImportService.ImportFromFileAsync(file.Path, SelectedYear, ct)
            };

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

            var scheduleLabel = SelectedScheduleType switch
            {
                ScheduleType.Dmepos => "DMEPOS",
                ScheduleType.PfsNational => "PFS",
                ScheduleType.ClinicalLab => "CLFS",
                ScheduleType.AspDrug => "ASP",
                ScheduleType.Opps => "OPPS",
                ScheduleType.Asc => "ASC",
                _ => "FeeSchedule"
            };
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

            var exporter = SelectedScheduleType switch
            {
                ScheduleType.Dmepos => _dmeposExportService,
                ScheduleType.PfsNational => _pfsExportService,
                ScheduleType.ClinicalLab => _clfsExportService,
                ScheduleType.AspDrug => _aspExportService,
                ScheduleType.Opps => _oppsExportService,
                ScheduleType.Asc => _ascExportService,
                _ => _dmeposExportService
            };

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
            IReadOnlyList<int> years = SelectedScheduleType switch
            {
                ScheduleType.Dmepos => await Task.Run(() => _dmeposRepository.GetAvailableYears()),
                ScheduleType.PfsNational => await Task.Run(() => _pfsRepository.GetAvailableYears()),
                ScheduleType.ClinicalLab => await Task.Run(() => _clfsRepository.GetAvailableYears()),
                ScheduleType.AspDrug => await Task.Run(() => _aspRepository.GetAvailableYears()),
                ScheduleType.Opps => await Task.Run(() => _oppsRepository.GetAvailableYears()),
                ScheduleType.Asc => await Task.Run(() => _ascRepository.GetAvailableYears()),
                _ => await Task.Run(() => _dmeposRepository.GetAvailableYears())
            };

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
