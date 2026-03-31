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
        var clfsRepo = new ClfsRepository(dbContext);
        var aspRepo = new AspRepository(dbContext);
        var oppsRepo = new OppsRepository(dbContext);
        var ascRepo = new AscRepository(dbContext);
        var importLogRepo = new ImportLogRepository(dbContext);

        var dmeposImportService = new DmeposImportService(dmeposRepo, importLogRepo);
        var pfsImportService = new PfsImportService(pfsRepo, importLogRepo);
        var clfsImportService = new ClfsImportService(clfsRepo, importLogRepo);
        var aspImportService = new AspImportService(aspRepo, importLogRepo);
        var oppsImportService = new OppsImportService(oppsRepo, importLogRepo);
        var ascImportService = new AscImportService(ascRepo, importLogRepo);

        var dmeposExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.Dmepos, dmeposRepo);
        var pfsExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.PfsNational, pfsRepo);
        var clfsExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.ClinicalLab, clfsRepo);
        var aspExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.AspDrug, aspRepo);
        var oppsExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.Opps, oppsRepo);
        var ascExportService = new FeeExportService(CMSFeeApp.Core.FeeScheduleType.Asc, ascRepo);

        var dmeposSyncService = new DmeposCmsSyncService(httpClient, dmeposImportService, dmeposRepo, importLogRepo);
        var pfsSyncService = new PfsCmsSyncService(httpClient, pfsImportService, pfsRepo, importLogRepo);
        var clfsSyncService = new ClfsCmsSyncService(httpClient, clfsImportService, clfsRepo, importLogRepo);
        var aspSyncService = new AspCmsSyncService(httpClient, aspImportService, aspRepo, importLogRepo);
        var oppsSyncService = new OppsCmsSyncService(httpClient, oppsImportService, oppsRepo, importLogRepo);
        var ascSyncService = new AscCmsSyncService(httpClient, ascImportService, ascRepo, importLogRepo);

        var viewModel = new MainViewModel(
            updateService,
            dmeposRepo,
            pfsRepo,
            clfsRepo,
            aspRepo,
            oppsRepo,
            ascRepo,
            dmeposImportService,
            pfsImportService,
            clfsImportService,
            aspImportService,
            oppsImportService,
            ascImportService,
            dmeposExportService,
            pfsExportService,
            clfsExportService,
            aspExportService,
            oppsExportService,
            ascExportService,
            dmeposSyncService,
            pfsSyncService,
            clfsSyncService,
            aspSyncService,
            oppsSyncService,
            ascSyncService);

        _window = new MainWindow(viewModel);
        viewModel.Window = _window;
        _window.Activate();
    }

    private Window? _window;
}
