using System.Windows;
using IISDeploy.Core.Models;
using System.Windows.Media;

namespace IISDeploy.UI.Views;

public partial class ScanResultsWindow : Window
{
    public ScanResultsWindow(List<DependencyInfo> found, List<DependencyInfo> missing, TimeSpan duration)
    {
        InitializeComponent();

        var scannerErrors = found.Where(d => d.Type == "ScannerError").ToList();

        SummaryText.Text = $"Scan complete. Found: {found.Count}, Missing: {missing.Count}. Duration: {duration.TotalSeconds:F1}s";

        if (missing.Count == 0 && scannerErrors.Count == 0)
        {
            SummaryTitle.Text = "All Dependencies Met";
            SummaryTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AlertSuccessText");
            SummaryText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AlertSuccessText");
            SummaryAlert.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "AlertSuccessBackground");
            SummaryAlert.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AlertSuccessText");
        }
        else if (missing.Count > 0)
        {
            SummaryTitle.Text = "Missing Dependencies Detected";
        }

        MissingList.ItemsSource = missing;
        WarningList.ItemsSource = scannerErrors;

        if (scannerErrors.Count == 0)
            WarningsBorder.Visibility = Visibility.Collapsed;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
