using CMSFeeApp.Core.Services;
using CMSFeeApp.Data;
using CMSFeeApp.Data.Repositories;
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
        var updateService = new UpdateService(httpClient);
        var dmeposRepo = new DmeposRepository(dbContext);
        var pfsRepo = new PfsRepository(dbContext);

        var viewModel = new MainViewModel(updateService, dmeposRepo, pfsRepo);

        _window = new MainWindow(viewModel);
        _window.Activate();
    }

    private Window? _window;
}
