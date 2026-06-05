using System.Windows;
using System.Windows.Media;

namespace IISDeploy.UI.Views;

public enum DialogType
{
    Info,
    Success,
    Warning,
    Error,
    Question
}

public partial class CustomDialog : Window
{
    public bool Result { get; private set; }

    public CustomDialog(string title, string message, DialogType type, string? detail = null)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        if (!string.IsNullOrWhiteSpace(detail))
        {
            DetailsExpander.Visibility = Visibility.Visible;
            DetailsText.Text = detail;
        }

        var app = System.Windows.Application.Current;
        Brush GetBrush(string key) => (Brush)app.Resources[key];

        switch (type)
        {
            case DialogType.Info:
                IconText.Text = "ℹ";
                IconText.Foreground = GetBrush("PrimaryBrush");
                break;
            case DialogType.Success:
                IconText.Text = "✓";
                IconText.Foreground = GetBrush("SuccessBrush");
                break;
            case DialogType.Warning:
                IconText.Text = "⚠";
                IconText.Foreground = GetBrush("WarningBrush");
                break;
            case DialogType.Error:
                IconText.Text = "❌";
                IconText.Foreground = GetBrush("ErrorBrush");
                break;
            case DialogType.Question:
                IconText.Text = "?";
                IconText.Foreground = GetBrush("PrimaryBrush");
                CancelBtn.Visibility = Visibility.Visible;
                OkBtn.Content = "Yes";
                break;
        }
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
        Close();
    }

    public static bool Show(string title, string message, DialogType type, string? detail = null, Window? owner = null)
    {
        var dialog = new CustomDialog(title, message, type, detail)
        {
            Owner = owner ?? System.Windows.Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }
}
