# CMS Fee Schedule Viewer (WinUI 3)

A Windows-only desktop application for viewing CMS (Centers for Medicare & Medicaid Services) fee schedules, including DMEPOS and Physician Fee Schedule (PFS) National data. Built with WinUI 3 and distributed as a portable ZIP — no installation or administrator rights required.

## Features

- **DMEPOS Fee Schedule** — browse allowable amounts by state, year, and HCPCS code
- **PFS National Fee Schedule** — view both facility and non-facility payment amounts
- **Filters** — search by HCPCS code and description keyword
- **Update notifications** — automatic check against GitHub Releases on startup; shows a dismissible banner if a newer version is available
- **Portable** — runs from any folder; database stored in `%LOCALAPPDATA%\CMSFeeApp\data\`

## Solution Structure

```
CMSFeeApp.slnx
├── src/
│   ├── CMSFeeApp.Core/          # Models, interfaces, UpdateService  (net8.0)
│   ├── CMSFeeApp.Data/          # SQLite + migrations + repositories (net8.0)
│   └── CMSFeeApp.WinUI/         # WinUI 3 desktop app               (net8.0-windows10.0.19041.0)
└── tests/
    └── CMSFeeApp.Tests/         # xUnit unit tests                   (net8.0)
```

## Prerequisites (Development)

| Requirement | Version |
|---|---|
| Windows | 10 version 1903 (build 18362) or later |
| Visual Studio | 2022 17.10+ with **Windows App SDK / WinUI** workload |
| .NET SDK | 8.0 |
| Windows App SDK | 1.6 (installed automatically via NuGet) |

> **Tip:** Install the "Windows App SDK C# Templates" VS extension if the `WinUI 3` project type is not shown in the New Project dialog.

## How to Build

### Visual Studio

1. Open `CMSFeeApp.slnx` in Visual Studio 2022.
2. Select **Build → Restore NuGet Packages**.
3. Set the startup project to `CMSFeeApp.WinUI`.
4. Press **F5** to build and run.

### Command Line

```powershell
# Restore packages
dotnet restore

# Build (WinUI project requires Windows)
dotnet build src/CMSFeeApp.WinUI/CMSFeeApp.WinUI.csproj -c Release

# Run tests (cross-platform; Core and Data only)
dotnet test tests/CMSFeeApp.Tests/CMSFeeApp.Tests.csproj
```

## How to Publish (Self-Contained win-x64)

Run the following from the repository root on a Windows machine:

```powershell
dotnet publish src/CMSFeeApp.WinUI/CMSFeeApp.WinUI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -o publish/win-x64
```

> `PublishSingleFile=false` is recommended for WinUI 3 apps because the Windows App SDK runtime DLLs must be present alongside the executable.
> `PublishTrimmed=false` avoids trimming issues with WinUI reflection-heavy APIs.

## How to Create a Portable ZIP

After publishing, zip the output folder:

```powershell
Compress-Archive -Path publish/win-x64/* -DestinationPath dist/CMSFeeApp-win-x64.zip
```

Distribute `CMSFeeApp-win-x64.zip`. Users unzip anywhere and double-click `CMSFeeApp.WinUI.exe`. No installer, no admin rights needed.

### What's included in the ZIP

- `CMSFeeApp.WinUI.exe` — main executable
- `CMSFeeApp.Core.dll`, `CMSFeeApp.Data.dll` — supporting libraries
- Windows App SDK runtime DLLs (`Microsoft.WindowsAppRuntime.*`, `WinRT.Runtime.dll`, etc.)
- `Microsoft.Data.Sqlite.dll` and native SQLite binary

### Database location

The SQLite database is created automatically on first launch at:

```
%LOCALAPPDATA%\CMSFeeApp\data\cms_fees.db
```

Users can back up or move this file without affecting the application.

## Database Schema

Managed via an embedded migration system (`CMSFeeApp.Data.MigrationRunner`). Migrations run automatically on startup.

| Table | Purpose |
|---|---|
| `migrations` | Tracks applied migrations |
| `states` | US state lookup |
| `selected_states` | User-selected states for filtering |
| `user_preferences` | Key-value app settings |
| `import_log` | History of data imports |
| `dmepos_fees` | DMEPOS fee records (code, state, year, allowable) |
| `pfs_fees` | PFS National fee records (code, year, non-facility, facility) |

## Running Tests

Tests cover the migration runner and update service (mocked HTTP). They run on any platform:

```bash
dotnet test tests/CMSFeeApp.Tests/CMSFeeApp.Tests.csproj
```

## Architecture Notes

- **MVVM** via [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — `[ObservableProperty]` and `[RelayCommand]` source generators
- **Update check** — `UpdateService` calls the GitHub Releases API for `cjlitson/CMS-fee-app-winui`, compares semantic versions, and surfaces a dismissible `InfoBar` banner in the UI
- **Phase 2 stubs** — Sync, Import, and Export buttons are wired to commands that display a status message; real CMS file downloaders/parsers will be added in Phase 2