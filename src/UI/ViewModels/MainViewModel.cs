using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using IISDeploy.Application.DTOs;
using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Iis;
using IISDeploy.Infrastructure.Packaging;
using IISDeploy.UI.Mvvm;
using IISDeploy.UI.Services;
using IISDeploy.UI.Views;
using Microsoft.Win32;

namespace IISDeploy.UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly IExportOrchestrator _exportOrchestrator;
    private readonly IImportOrchestrator _importOrchestrator;
    private readonly IDependencyAnalysisService _dependencyService;
    private readonly IReportGeneratorService _reportGenerator;

    public MainViewModel(
        IDashboardService dashboardService,
        IExportOrchestrator exportOrchestrator,
        IImportOrchestrator importOrchestrator,
        IDependencyAnalysisService dependencyService,
        IReportGeneratorService reportGenerator)
    {
        _dashboardService = dashboardService;
        _exportOrchestrator = exportOrchestrator;
        _importOrchestrator = importOrchestrator;
        _dependencyService = dependencyService;
        _reportGenerator = reportGenerator;

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        ExportCommand = new RelayCommand(async () => await ExportAsync());
        ImportCommand = new RelayCommand(async () => await ImportAsync());
        ScanDependenciesCommand = new RelayCommand(async () => await ScanDependenciesAsync());
    }

    private ServerSummary? _serverSummary;
    public ServerSummary? ServerSummary
    {
        get => _serverSummary;
        set => SetProperty(ref _serverSummary, value);
    }

    private ObservableCollection<SiteTreeNode> _sites = [];
    public ObservableCollection<SiteTreeNode> Sites
    {
        get => _sites;
        set => SetProperty(ref _sites, value);
    }

    private ObservableCollection<AppPoolSummary> _appPools = [];
    public ObservableCollection<AppPoolSummary> AppPools
    {
        get => _appPools;
        set => SetProperty(ref _appPools, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ImportCommand).RaiseCanExecuteChanged();
        }
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            _ = FilterSitesAsync();
        }
    }

    private bool _exportIncludeCertificates;
    public bool ExportIncludeCertificates
    {
        get => _exportIncludeCertificates;
        set => SetProperty(ref _exportIncludeCertificates, value);
    }

    private string _exportOutputPath = string.Empty;
    public string ExportOutputPath
    {
        get => _exportOutputPath;
        set => SetProperty(ref _exportOutputPath, value);
    }

    private CancellationTokenSource? _exportCts;

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ScanDependenciesCommand { get; }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading IIS configuration...";

        try
        {
            ServerSummary = await _dashboardService.GetServerSummaryAsync();
            var tree = await _dashboardService.GetSiteTreeAsync();
            Sites = new ObservableCollection<SiteTreeNode>(tree);
            var pools = await _dashboardService.GetAppPoolSummariesAsync();
            AppPools = new ObservableCollection<AppPoolSummary>(pools);
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FilterSitesAsync()
    {
        var tree = await _dashboardService.GetSiteTreeAsync(_searchText);
        Sites = new ObservableCollection<SiteTreeNode>(tree);
    }

    private static List<string> GetCheckedSiteNames(IEnumerable<SiteTreeNode> nodes)
    {
        var names = new List<string>();
        foreach (var node in nodes)
        {
            if (node.IsChecked)
                names.Add(node.Name);
            names.AddRange(GetCheckedSiteNames(node.Children));
        }
        return names;
    }

    private async Task ExportAsync()
    {
        var selectedNames = GetCheckedSiteNames(Sites);

        if (selectedNames.Count == 0)
        {
            CustomDialog.Show("No Sites Selected", 
                "Please select at least one site to export by checking the checkbox next to the site name.", 
                DialogType.Info);
            StatusMessage = "No sites selected for export.";
            return;
        }

        // Build intelligent file name
        var siteLabel = selectedNames.Count == 1
            ? selectedNames[0]
            : $"{selectedNames.Count}sites";
        var defaultName = $"IISExport_{siteLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.iispackage";

        var dialog = new SaveFileDialog
        {
            Title = "Save IIS Export Package",
            Filter = "IIS Package (*.iispackage)|*.iispackage|All Files (*.*)|*.*",
            DefaultExt = ".iispackage",
            FileName = defaultName
        };

        if (dialog.ShowDialog() != true)
            return;

        // Show export preview confirmation
        var siteList = string.Join("\n", selectedNames.Select(n => $"  • {n}"));
        var previewMsg = $"You are about to export {selectedNames.Count} site(s):\n\n{siteList}\n\nOutput: {dialog.FileName}\n\nContinue?";
        
        if (!CustomDialog.Show("Export Preview", previewMsg, DialogType.Question))
            return;

        ExportOutputPath = dialog.FileName;
        _exportCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusMessage = "Exporting...";

        var progress = new Progress<Core.Models.OperationProgress>(p =>
        {
            ProgressValue = p.Percentage;
            StatusMessage = $"{p.CurrentStep} ({p.CompletedItems}/{p.TotalItems})";

            if (p.Status == Core.Models.Enums.OperationStatus.Failed)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    CustomDialog.Show("Export Error", $"Export failed: {p.CurrentStep}", DialogType.Error));
            }
        });

        try
        {
            var request = new ExportRequest
            {
                SiteNames = selectedNames,
                OutputPath = ExportOutputPath,
                Mode = Core.Models.Enums.ExportMode.MultiSite,
                IncludeCertificates = ExportIncludeCertificates,
                CertificatePassword = null,
                IncludeNtfsPermissions = false,
                RunDependencyScan = true
            };

            var result = await _exportOrchestrator.ExportAsync(request, _exportCts.Token, progress);

            if (result.Success)
            {
                StatusMessage = $"Export complete — {result.PackagePath}";
                // ToastNotificationService.Show($"Export complete: {Path.GetFileName(result.PackagePath)}",
                //     ToastType.Success);
                CustomDialog.Show("Export Complete", 
                    $"Export completed successfully!\n\nPackage: {result.PackagePath}\nDuration: {result.Duration.TotalSeconds:F1}s", 
                    DialogType.Success);
            }
            else
            {
                StatusMessage = $"Export failed: {string.Join("; ", result.Errors)}";
                // ToastNotificationService.Show("Export failed", ToastType.Error);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    private CancellationTokenSource? _importCts;

    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open IIS Import Package",
            Filter = "IIS Package (*.iispackage)|*.iispackage|All Files (*.*)|*.*",
            DefaultExt = ".iispackage"
        };

        if (dialog.ShowDialog() != true)
            return;

        _importCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusMessage = "Analyzing package...";

        try
        {
            // Read manifest and check conflicts first
            var packagePath = dialog.FileName;
            var packageBuilder = new ZipPackageBuilderService();
            var manifest = await packageBuilder.ReadManifestAsync(packagePath);

            // Extract and read site/pool data for conflict check
            var extractionPath = Path.Combine(Path.GetTempPath(), $"iisdeploy_preview_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(extractionPath);
                System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, extractionPath, overwriteFiles: true);

                var sites = LoadPreviewJson<IisSite>(
                    Path.Combine(extractionPath, "sites"), "site.json");
                var pools = LoadPreviewJson<IisApplicationPool>(
                    Path.Combine(extractionPath, "apppools"), "apppool.json");

                var discoveryService = new IisDiscoveryService();
                var bindingManager = new BindingManagerService(discoveryService);
                var conflictResolver = new ConflictResolutionService(discoveryService, bindingManager);

                var conflictReport = await conflictResolver.AnalyzeImportConflictsAsync(sites, pools);

                // Show preview dialog
                var previewVm = new ImportPreviewViewModel(manifest, sites, pools, conflictReport);
                var previewWindow = new ImportPreviewWindow(previewVm)
                {
                    Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? System.Windows.Application.Current.MainWindow
                };
                var result = previewWindow.ShowDialog();

                if (result != true || !previewVm.Confirmed)
                {
                    StatusMessage = "Import cancelled.";
                    IsBusy = false;
                    return;
                }

                var strategies = previewVm.GetConflictStrategies();
                var pathOverrides = previewVm.GetPathOverrides();
                var customNames = previewVm.GetCustomNames();
                var customPorts = previewVm.GetCustomPorts();
                IsBusy = true;
                StatusMessage = "Importing...";

                var progress = new Progress<Core.Models.OperationProgress>(p =>
                {
                    ProgressValue = p.Percentage;
                    StatusMessage = $"{p.CurrentStep} ({p.CompletedItems}/{p.TotalItems})";
                });

                var request = new ImportRequest
                {
                    PackagePath = packagePath,
                    DryRun = false,
                    AutoRemediate = false,
                    ConflictStrategies = strategies,
                    SitePathOverrides = pathOverrides,
                    CustomNames = customNames,
                    CustomPorts = customPorts
                };

                var importResult = await _importOrchestrator.ImportAsync(request, _importCts.Token, progress);

                if (importResult.Success && importResult.Report?.Status != Core.Models.Enums.OperationStatus.Failed)
                {
                    StatusMessage = "Import complete.";
                    var reportSummary = string.Join("\n", importResult.Report?.Summary ?? []);
                    var warnings = importResult.Report?.Entries
                        .Where(e => !e.Success)
                        .Select(e => $"{e.Category}/{e.Item}: {e.Message}")
                        .ToList() ?? [];
                    var warnText = warnings.Count > 0
                        ? $"\n\nWarnings:\n{string.Join("\n", warnings)}"
                        : "";
                    CustomDialog.Show("Import Complete", 
                        $"Import completed successfully!\n\n{reportSummary}{warnText}\nDuration: {importResult.Duration.TotalSeconds:F1}s", 
                        DialogType.Success);
                    await RefreshAsync();
                }
                else
                {
                    var errors = importResult.Errors.Count > 0
                        ? string.Join("\n", importResult.Errors)
                        : string.Join("\n", importResult.Report?.Summary ?? ["Unknown error"]);
                    var entries = importResult.Report?.Entries
                        .Select(e => $"[{(e.Success ? "OK" : "FAIL")}] {e.Category}/{e.Item}: {e.Message}")
                        .ToList() ?? [];
                    var allDetails = errors;
                    if (entries.Count > 0)
                        allDetails += $"\n\nDetails:\n{string.Join("\n", entries)}";
                    StatusMessage = $"Import failed";
                    CustomDialog.Show("Import Result", "Import completed with issues.", DialogType.Warning, allDetails);
                }
            }
            finally
            {
                try { if (Directory.Exists(extractionPath)) Directory.Delete(extractionPath, recursive: true); }
                catch { }
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Import cancelled.";
        }
        catch (Exception ex)
        {
            var fullMsg = $"Error: {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException is not null)
                fullMsg += $"\n\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            fullMsg += $"\n\nStack: {ex.StackTrace}";
            StatusMessage = $"Import error: {ex.Message}";
            CustomDialog.Show("Import Failed", ex.Message, DialogType.Error, fullMsg);
        }
        finally
        {
            IsBusy = false;
            _importCts?.Dispose();
            _importCts = null;
        }
    }

    private static List<T> LoadPreviewJson<T>(string directory, string fileName)
    {
        var results = new List<T>();
        if (!Directory.Exists(directory)) return results;
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var filePath = Path.Combine(subDir, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var obj = System.Text.Json.JsonSerializer.Deserialize<T>(json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (obj is not null) results.Add(obj);
                }
                catch { }
            }
        }
        return results;
    }

    private CancellationTokenSource? _scanCts;
    private ObservableCollection<Core.Models.DependencyInfo> _scanResults = [];
    public ObservableCollection<Core.Models.DependencyInfo> ScanResults
    {
        get => _scanResults;
        set => SetProperty(ref _scanResults, value);
    }

    private async Task ScanDependenciesAsync()
    {
        _scanCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressValue = 0;
        StatusMessage = "Scanning dependencies...";

        var progress = new Progress<Core.Models.OperationProgress>(p =>
        {
            ProgressValue = p.Percentage;
            StatusMessage = $"{p.CurrentStep}";
        });

        try
        {
            var request = new DependencyScanRequest
            {
                FullServerScan = true
            };

            var result = await _dependencyService.ScanAsync(request, _scanCts.Token, progress);

            ScanResults = new ObservableCollection<Core.Models.DependencyInfo>(result.FoundDependencies);

            var missing = result.MissingDependencies;
            var scannerErrors = result.FoundDependencies
                .Where(d => d.Type == "ScannerError")
                .ToList();

            var summary = $"Scan complete. Found: {result.FoundDependencies.Count} dependencies, " +
                $"Missing: {missing.Count}. Duration: {result.ScanDuration.TotalSeconds:F1}s";

            var scanWindow = new ScanResultsWindow(result.FoundDependencies, missing, result.ScanDuration)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            scanWindow.ShowDialog();

            StatusMessage = summary;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Dependency scan cancelled.";
        }
        catch (Exception ex)
        {
            var fullMsg = $"Error: {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException is not null)
                fullMsg += $"\n\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            fullMsg += $"\n\nStack: {ex.StackTrace}";
            StatusMessage = $"Scan error: {ex.Message}";
            CustomDialog.Show("Scan Error", "Dependency scan failed.", DialogType.Error, fullMsg);
        }
        finally
        {
            IsBusy = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }
}
