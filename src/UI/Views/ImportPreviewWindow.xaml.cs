using System.Windows;
using IISDeploy.UI.ViewModels;

namespace IISDeploy.UI.Views;

public partial class ImportPreviewWindow : Window
{
    public ImportPreviewWindow(ImportPreviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            if (viewModel is not null)
            {
                viewModel.ImportRequested += () =>
                {
                    viewModel.Confirmed = true;
                    DialogResult = true;
                    Close();
                };
                viewModel.CancelRequested += () =>
                {
                    viewModel.Confirmed = false;
                    DialogResult = false;
                    Close();
                };
            }
        };
    }
}
