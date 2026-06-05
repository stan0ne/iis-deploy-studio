using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace IISDeploy.UI.Views;

public partial class LogViewerWindow : Window
{
    private readonly ObservableCollection<string> _allLines = [];
    private readonly string _logPath;

    public LogViewerWindow()
    {
        InitializeComponent();
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        if (Directory.Exists(_logPath))
        {
            var logFile = Directory.GetFiles(_logPath, "iisdeploy-*.log")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (logFile is not null)
                LoadLogFile(logFile);
        }

        LogList.ItemsSource = _allLines;
    }

    private void LoadLogFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _allLines.Add(line);
            }
        }
        catch (Exception ex)
        {
            _allLines.Add($"[ERROR] Could not load log file: {ex.Message}");
        }
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        _allLines.Clear();
        if (Directory.Exists(_logPath))
        {
            var logFile = Directory.GetFiles(_logPath, "iisdeploy-*.log")
                .OrderByDescending(f => f)
                .FirstOrDefault();
            if (logFile is not null)
                LoadLogFile(logFile);
        }
        ApplyFilter();
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        _allLines.Clear();
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox.Text;
        if (string.IsNullOrWhiteSpace(filter))
        {
            LogList.ItemsSource = _allLines;
        }
        else
        {
            LogList.ItemsSource = _allLines
                .Where(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (AutoScrollChk.IsChecked == true && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
