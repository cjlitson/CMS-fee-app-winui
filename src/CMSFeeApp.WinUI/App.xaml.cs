using CMSFeeApp.Core.Services;
using CMSFeeApp.Data;
using CMSFeeApp.Data.Repositories;
using CMSFeeApp.Data.Services;
using CMSFeeApp.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace CMSFeeApp.WinUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dbPath = DatabaseContext.GetDefaultDatabasePath();
        var dbContext = new DatabaseContext(dbPath);
        var migrationRunner = new MigrationRunner(dbContext);
        migrationRunner.RunMigrations();

        var httpClient = new System.Net.Http.HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CMSFeeApp/1.0");

        var updateService = new UpdateService(httpClient);
        var dmeposRepo = new DmeposRepository(dbContext);
        var pfsRepo = new PfsRepository(dbContext);
        var importLogRepo = new ImportLogRepository(dbContext);

        var dmeposImportService = new DmeposImportService(dmeposRepo, importLogRepo);
        var pfsImportService = new PfsImportService(pfsRepo, importLogRepo);

        var dmeposExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.Dmepos, dmeposRepo);
        var pfsExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.PfsNational, pfsRepo);

        var dmeposSyncService = new DmeposCmsSyncService(httpClient, dmeposImportService, dmeposRepo, importLogRepo);
        var pfsSyncService = new PfsCmsSyncService(httpClient, pfsImportService, pfsRepo, importLogRepo);

        var viewModel = new MainViewModel(
            updateService,
            dmeposRepo,
            pfsRepo,
            dmeposImportService,
            pfsImportService,
            dmeposExportService,
            pfsExportService,
            dmeposSyncService,
            pfsSyncService);

        _window = new MainWindow(viewModel);
        viewModel.Window = _window;
        _window.Activate();
    }

    private Window? _window;
}
