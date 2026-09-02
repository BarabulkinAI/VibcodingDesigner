using Avalonia.Controls;
using Avalonia.Interactivity;
using ReportDesigner.UI.ViewModels;
using ReportDesigner.ViewModels;

namespace ReportDesigner.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _closeConfirmed;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.PreviewChanged += () => PreviewImage.Source = _vm.PreviewImage;
        PreviewImage.Source = _vm.PreviewImage;
        Closing += OnClosing;
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => _vm.RefreshPreview();

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;

        e.Cancel = true;
        if (await _vm.TryPrepareForCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
