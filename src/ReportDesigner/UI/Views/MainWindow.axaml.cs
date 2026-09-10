using System.ComponentModel;
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
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        RefreshRecentFilesMenu();
        // У ScrollViewer нет биндируемого свойства текущего Offset (только Offset для
        // управления им извне) — линейки узнают о прокрутке канваса только так.
        CanvasScrollViewer.ScrollChanged += (_, _) =>
        {
            HorizontalRuler.ScrollOffset = CanvasScrollViewer.Offset.X;
            VerticalRuler.ScrollOffset = CanvasScrollViewer.Offset.Y;
        };
        Closing += OnClosing;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RecentFiles))
            RefreshRecentFilesMenu();
        if (e.PropertyName == nameof(MainViewModel.IsPreviewVisible))
            UpdatePreviewColumnWidths();
    }

    /// <summary>
    /// Схлопывает столбцы панели превью (сплиттер + сама панель) до 0, когда она скрыта — в
    /// отличие от простого IsVisible на содержимом, это не оставляет пустой промежуток на месте
    /// звёздочного столбца. Именованные ColumnDefinition вместо биндинга Width — по тому же
    /// принципу, что и RecentFilesMenuItem выше: явный код надёжнее для того, что плохо ложится
    /// на компилируемые биндинги.
    /// </summary>
    private void UpdatePreviewColumnWidths()
    {
        var visible = _vm.IsPreviewVisible;
        var columns = MainContentGrid.ColumnDefinitions;
        columns[5].Width = visible ? new GridLength(4) : new GridLength(0);
        columns[6].Width = visible ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
    }

    /// <summary>
    /// Пункты «Недавние файлы» собираются вручную в code-behind, а не через
    /// MenuItem.ItemsSource + DataTemplate — с динамическим списком путей и компилируемыми
    /// биндингами (x:DataType на весь файл) это потребовало бы ссылки на VM-команду из шаблона с
    /// DataContext = string через $parent-навигацию; явное построение здесь проще и надёжнее.
    /// </summary>
    private void RefreshRecentFilesMenu()
    {
        RecentFilesMenuItem.Items.Clear();

        if (_vm.RecentFiles.Count == 0)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem { Header = "(пусто)", IsEnabled = false });
            return;
        }

        foreach (var path in _vm.RecentFiles)
        {
            RecentFilesMenuItem.Items.Add(new MenuItem
            {
                Header = path,
                Command = _vm.OpenRecentCommand,
                CommandParameter = path,
            });
        }
    }

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
