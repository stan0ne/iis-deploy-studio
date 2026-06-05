using System.Windows;
using System.Windows.Media;

namespace IISDeploy.UI.Services;

public class ThemeService
{
    private bool _isDarkMode = false; // default light

    public bool IsDarkMode => _isDarkMode;

    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        ApplyTheme();
    }

    public void SetTheme(bool dark)
    {
        _isDarkMode = dark;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;

        var palettePath = _isDarkMode
            ? "Styles/DarkPalette.xaml"
            : "Styles/LightPalette.xaml";

        // Find and replace the palette dictionary
        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Palette.xaml") == true);

        if (existing != null)
            merged.Remove(existing);

        var newPalette = new ResourceDictionary
        {
            Source = new Uri(palettePath, UriKind.Relative)
        };

        // Insert at position 0 so Theme.xaml picks it up via DynamicResource
        merged.Insert(0, newPalette);
    }
}
