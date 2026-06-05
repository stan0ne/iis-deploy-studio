using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace IISDeploy.UI.Services;

public static class ToastNotificationService
{
    private const int MaxToasts = 4;

    public static void Show(string message, ToastType type = ToastType.Info, int durationMs = 4000)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current.MainWindow;
            if (window is null) return;

            var toast = CreateToast(message, type);
            AddToWindow(window, toast);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                RemoveToast(window, toast);
            };
            timer.Start();
        });
    }

    private static Border CreateToast(string message, ToastType type)
    {
        var color = type switch
        {
            ToastType.Success => Color.FromRgb(0x10, 0x7C, 0x10),
            ToastType.Warning => Color.FromRgb(0xFF, 0x8C, 0x00),
            ToastType.Error => Color.FromRgb(0xD1, 0x34, 0x38),
            _ => Color.FromRgb(0x00, 0x78, 0xD4)
        };

        var border = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 4, 0, 0),
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360
            },
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, -20)
        };

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        var slideIn = new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(200));
        border.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        border.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);

        return border;
    }

    private static void AddToWindow(Window window, Border toast)
    {
        var panel = window.Content as Grid;
        if (panel is null) return;

        var overlay = panel.Children.OfType<StackPanel>()
            .FirstOrDefault(sp => sp.Tag as string == "__ToastPanel");

        if (overlay is null)
        {
            overlay = new StackPanel
            {
                Tag = "__ToastPanel",
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 20, 20)
            };
            panel.Children.Add(overlay);
        }

        overlay.Children.Insert(0, toast);

        while (overlay.Children.Count > MaxToasts)
            overlay.Children.RemoveAt(overlay.Children.Count - 1);
    }

    private static void RemoveToast(Window window, Border toast)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        fadeOut.Completed += (s, e) =>
        {
            if (window.Content is Grid panel)
            {
                var overlay = panel.Children.OfType<StackPanel>()
                    .FirstOrDefault(sp => sp.Tag as string == "__ToastPanel");
                overlay?.Children.Remove(toast);
            }
        };
        toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}
