using Avalonia.Controls;
using Avalonia.Interactivity;
using ReportDesigner.UI.ViewModels;
using ReportDesigner.ViewModels;

namespace ReportDesigner.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.PreviewChanged += () => PreviewImage.Source = _vm.PreviewImage;
        PreviewImage.Source = _vm.PreviewImage;
    }

    private void OnRefresh(object? sender, RoutedEventArgs e) => _vm.RefreshPreview();
}