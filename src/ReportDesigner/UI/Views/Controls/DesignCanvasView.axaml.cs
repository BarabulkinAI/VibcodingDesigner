using Avalonia.Controls;
using Avalonia.Interactivity;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Views.Controls;

public partial class DesignCanvasView : UserControl
{
    public DesignCanvasView()
    {
        InitializeComponent();
        // У ScrollViewer нет биндируемого свойства текущего Offset (только Offset для
        // управления им извне) — линейки узнают о прокрутке канваса только так.
        CanvasScrollViewer.ScrollChanged += (_, _) =>
        {
            HorizontalRuler.ScrollOffset = CanvasScrollViewer.Offset.X;
            VerticalRuler.ScrollOffset = CanvasScrollViewer.Offset.Y;
        };
    }

    /// <summary>Кнопка вкладки хранит имя страницы в Tag — так не нужен $parent-биндинг на команду
    /// VM из шаблона элемента (тот же приём, что и у «Недавних файлов» в MainWindow).</summary>
    private void OnPageTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string name } && DataContext is DesignSurfaceViewModel vm)
            vm.SelectPageCommand.Execute(name);
    }
}
