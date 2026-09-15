using Avalonia.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Views.Controls;

public partial class PreviewPanelView : UserControl
{
    public PreviewPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        vm.PreviewChanged += () => PreviewImage.Source = vm.PreviewImage;
        PreviewImage.Source = vm.PreviewImage;
    }
}
