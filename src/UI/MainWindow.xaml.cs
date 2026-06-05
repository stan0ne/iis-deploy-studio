using IISDeploy.UI.Services;
using IISDeploy.UI.ViewModels;
using IISDeploy.UI.Views;
using System.Windows;
using System.Windows.Controls;

namespace IISDeploy.UI;

public partial class MainWindow : Window
{
    private readonly ThemeService _themeService = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;

        // Start with light mode active
        _themeService.SetTheme(false);
        UpdateThemeIcon();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
            // ToastNotificationService.Show("IISDeploy Studio ready", ToastType.Info, 2000);
        }
    }

    // ── Theme Toggle ──────────────────────────────────────
    private void DarkModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _themeService.ToggleTheme();
        UpdateThemeIcon();
        // ToastNotificationService.Show(
        //     _themeService.IsDarkMode ? "Dark mode enabled" : "Light mode enabled",
        //     ToastType.Info, 1500);
    }

    private void UpdateThemeIcon()
    {
        if (ThemeIcon is not null)
            ThemeIcon.Text = _themeService.IsDarkMode ? "☀" : "🌙";
    }

    // ── Sub Windows ───────────────────────────────────────
    private void LogViewBtn_Click(object sender, RoutedEventArgs e)
    {
        var viewer = new LogViewerWindow { Owner = this };
        viewer.Show();
    }

    private void ReportViewBtn_Click(object sender, RoutedEventArgs e)
    {
        var viewer = new ReportViewerWindow { Owner = this };
        viewer.Show();
    }
}
