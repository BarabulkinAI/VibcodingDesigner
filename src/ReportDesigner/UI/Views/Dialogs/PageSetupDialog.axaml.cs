using Avalonia.Controls;
using Avalonia.Interactivity;
using ReportDesigner.Models;

namespace ReportDesigner.UI.Views.Dialogs;

public partial class PageSetupDialog : Window
{
    public PageSetupDialog() : this(PageSizePreset.A4, false)
    {
    }

    public PageSetupDialog(PageSizePreset currentPreset, bool currentLandscape)
    {
        InitializeComponent();

        (currentPreset == PageSizePreset.A3 ? A3RadioButton : A4RadioButton).IsChecked = true;
        (currentLandscape ? LandscapeRadioButton : PortraitRadioButton).IsChecked = true;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var preset = A3RadioButton.IsChecked == true ? PageSizePreset.A3 : PageSizePreset.A4;
        var landscape = LandscapeRadioButton.IsChecked == true;
        Close((preset, landscape));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
