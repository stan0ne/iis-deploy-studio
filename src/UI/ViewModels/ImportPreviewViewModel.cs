using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using IISDeploy.Core.Models;
using IISDeploy.Core.Models.Enums;
using IISDeploy.UI.Mvvm;

namespace IISDeploy.UI.ViewModels;

public class ImportPreviewViewModel : ObservableObject
{
    public event Action? ImportRequested;
    public event Action? CancelRequested;

    public ImportPreviewViewModel(
        PackageManifest manifest,
        List<IisSite> sites,
        List<IisApplicationPool> pools,
        ConflictReport conflictReport)
    {
        SourceMachine = manifest.SourceMachine.MachineName;
        SourceOs = manifest.SourceMachine.OsVersion;
        SourceIis = manifest.SourceIisInfo.IisVersion;
        ExportDate = manifest.ExportTimestamp.ToString("yyyy-MM-dd HH:mm");
        ExportedBy = manifest.ExportedBy;
        SiteCount = sites.Count;
        PoolCount = pools.Count;

        Sites = new ObservableCollection<string>(manifest.ExportedSiteNames);
        Pools = new ObservableCollection<string>(manifest.ExportedAppPoolNames);

        SitePaths = new ObservableCollection<SitePathEntry>();
        foreach (var site in sites)
        {
            var originalPath = site.PhysicalPath;
            var pathExists = !string.IsNullOrEmpty(originalPath)
                && Directory.Exists(Path.GetPathRoot(originalPath));
            var defaultPath = Path.Combine(
                Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
                "inetpub", site.Name);

            SitePaths.Add(new SitePathEntry
            {
                SiteName = site.Name,
                OriginalPath = originalPath,
                PathExists = pathExists,
                TargetPath = pathExists ? originalPath : defaultPath,
                CreateIfMissing = !pathExists
            });
        }

        Conflicts = new ObservableCollection<ConflictEntrySummary>();

        foreach (var c in conflictReport.SiteConflicts)
        {
            var entry = new ConflictEntrySummary
            {
                ObjectType = "Site",
                ObjectName = c.ObjectName,
                Message = c.Message,
                Strategy = ConflictResolutionStrategy.Rename
            };
            entry.SelectedStrategy = entry.AvailableStrategies.First(s => s.Strategy == ConflictResolutionStrategy.Rename);
            Conflicts.Add(entry);
        }
        foreach (var c in conflictReport.PoolConflicts)
        {
            var entry = new ConflictEntrySummary
            {
                ObjectType = "AppPool",
                ObjectName = c.ObjectName,
                Message = c.Message,
                Strategy = ConflictResolutionStrategy.Rename
            };
            entry.SelectedStrategy = entry.AvailableStrategies.First(s => s.Strategy == ConflictResolutionStrategy.Rename);
            Conflicts.Add(entry);
        }
        foreach (var c in conflictReport.BindingConflicts)
        {
            var entry = new ConflictEntrySummary
            {
                ObjectType = "Binding",
                ObjectName = c.ObjectName,
                Message = c.Message,
                Strategy = ConflictResolutionStrategy.ChangeBinding
            };
            entry.UseBindingStrategies();
            entry.SelectedStrategy = entry.AvailableStrategies.First(s => s.Strategy == ConflictResolutionStrategy.ChangeBinding);
            Conflicts.Add(entry);
        }

        HasConflicts = Conflicts.Count > 0;

        ImportCommand = new RelayCommand<object>(_ => ImportRequested?.Invoke());
        CancelCommand = new RelayCommand<object>(_ => CancelRequested?.Invoke());
    }

    public bool Confirmed { get; set; }

    public string SourceMachine { get; }
    public string SourceOs { get; }
    public string SourceIis { get; }
    public string ExportDate { get; }
    public string ExportedBy { get; }
    public int SiteCount { get; }
    public int PoolCount { get; }

    public ObservableCollection<string> Sites { get; }
    public ObservableCollection<string> Pools { get; }
    public ObservableCollection<SitePathEntry> SitePaths { get; }
    public ObservableCollection<ConflictEntrySummary> Conflicts { get; }

    private bool _hasConflicts;
    public bool HasConflicts
    {
        get => _hasConflicts;
        set => SetProperty(ref _hasConflicts, value);
    }

    public ICommand ImportCommand { get; }
    public ICommand CancelCommand { get; }

    public Dictionary<string, string> GetPathOverrides()
    {
        var dict = new Dictionary<string, string>();
        foreach (var sp in SitePaths)
        {
            if (!string.IsNullOrEmpty(sp.TargetPath))
                dict[sp.SiteName] = sp.TargetPath;
        }
        return dict;
    }

    public Dictionary<string, ConflictResolutionStrategy> GetConflictStrategies()
    {
        var dict = new Dictionary<string, ConflictResolutionStrategy>();
        foreach (var c in Conflicts)
        {
            var key = c.ObjectType switch
            {
                "Site" => $"site:{c.ObjectName}",
                "AppPool" => $"pool:{c.ObjectName}",
                "Binding" => $"binding:{c.ObjectName}",
                _ => c.ObjectName
            };
            dict[key] = c.Strategy;
        }
        return dict;
    }

    public Dictionary<string, string> GetCustomNames()
    {
        var dict = new Dictionary<string, string>();
        foreach (var c in Conflicts)
        {
            if (!string.IsNullOrWhiteSpace(c.CustomName))
                dict[c.ObjectName] = c.CustomName;
        }
        return dict;
    }

    public Dictionary<string, int> GetCustomPorts()
    {
        var dict = new Dictionary<string, int>();
        foreach (var c in Conflicts)
        {
            if (c.Strategy == ConflictResolutionStrategy.ChangeBinding
                && !string.IsNullOrWhiteSpace(c.CustomPort)
                && int.TryParse(c.CustomPort, out var port)
                && port > 0 && port < 65535)
            {
                dict[c.ObjectName] = port;
            }
        }
        return dict;
    }
}

public class SitePathEntry : ObservableObject
{
    public string SiteName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public bool PathExists { get; set; }

    private string _targetPath = string.Empty;
    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    private bool _createIfMissing;
    public bool CreateIfMissing
    {
        get => _createIfMissing;
        set
        {
            if (SetProperty(ref _createIfMissing, value) && !value && !PathExists)
            {
                TargetPath = Path.Combine(
                    Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
                    "inetpub", SiteName);
            }
        }
    }

    public string PathStatusText => PathExists
        ? "Mevcut"
        : "Yok - oluşturulacak";
}

public class ConflictEntrySummary : ObservableObject
{
    private readonly List<ConflictStrategyOption> _strategies;

    public string ObjectType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    private ConflictResolutionStrategy _strategy;
    public ConflictResolutionStrategy Strategy
    {
        get => _strategy;
        set
        {
            if (SetProperty(ref _strategy, value))
            {
                OnPropertyChanged(nameof(ShowCustomName));
                OnPropertyChanged(nameof(ShowCustomPort));
            }
        }
    }

    public bool ShowStrategies => true;
    public bool ShowCustomName => Strategy == ConflictResolutionStrategy.Rename
        || Strategy == ConflictResolutionStrategy.Clone;
    public bool ShowCustomPort => Strategy == ConflictResolutionStrategy.ChangeBinding;

    private string _customName = string.Empty;
    public string CustomName
    {
        get => _customName;
        set => SetProperty(ref _customName, value);
    }

    private string _customPort = string.Empty;
    public string CustomPort
    {
        get => _customPort;
        set => SetProperty(ref _customPort, value);
    }

    public List<ConflictStrategyOption> AvailableStrategies => _strategies;

    public ConflictEntrySummary()
    {
        _strategies = new()
        {
            new() { Strategy = ConflictResolutionStrategy.Rename, Label = "Yeni isimle oluştur" },
            new() { Strategy = ConflictResolutionStrategy.Overwrite, Label = "Üzerine yaz" },
            new() { Strategy = ConflictResolutionStrategy.Clone, Label = "Klonla" },
            new() { Strategy = ConflictResolutionStrategy.Skip, Label = "Atla" }
        };
    }

    public void UseBindingStrategies()
    {
        _strategies.Clear();
        _strategies.Add(new() { Strategy = ConflictResolutionStrategy.ChangeBinding, Label = "Port değiştir" });
        _strategies.Add(new() { Strategy = ConflictResolutionStrategy.Skip, Label = "Atla" });
    }

    private ConflictStrategyOption? _selectedStrategy;
    public ConflictStrategyOption? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            if (SetProperty(ref _selectedStrategy, value) && value is not null)
                Strategy = value.Strategy;
        }
    }
}

public class ConflictStrategyOption
{
    public ConflictResolutionStrategy Strategy { get; set; }
    public string Label { get; set; } = string.Empty;
    public override string ToString() => Label;
}
