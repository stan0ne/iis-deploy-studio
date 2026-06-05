using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace IISDeploy.UI.Views;

public partial class ReportViewerWindow : Window
{
    private readonly string _reportsDir;

    public ReportViewerWindow()
    {
        InitializeComponent();

        _reportsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IISDeployStudio", "reports");

        RefreshReportList();
    }

    private void RefreshReportList()
    {
        var reports = new ObservableCollection<ReportInfo>();
        if (Directory.Exists(_reportsDir))
        {
            foreach (var file in Directory.GetFiles(_reportsDir, "*.html")
                         .Select(f => new FileInfo(f))
                         .OrderByDescending(f => f.LastWriteTime))
            {
                reports.Add(new ReportInfo
                {
                    Name = file.Name,
                    Path = file.FullName,
                    Date = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                });
            }
        }

        ReportListBox.ItemsSource = reports;
    }

    private void OpenReportBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Report",
            Filter = "HTML Reports (*.html)|*.html|JSON Reports (*.json)|*.json|All Files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_reportsDir) ? _reportsDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dialog.ShowDialog() == true)
        {
            LoadReport(dialog.FileName);
        }
    }

    private void RefreshListBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshReportList();
    }

    private void ReportListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ReportListBox.SelectedItem is ReportInfo info)
        {
            LoadReport(info.Path);
        }
    }

    private void LoadReport(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                ReportBrowser.Navigate(new Uri(path));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load report: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private class ReportInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }
}
