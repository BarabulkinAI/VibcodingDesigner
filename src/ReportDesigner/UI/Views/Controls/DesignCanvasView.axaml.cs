using Avalonia.Controls;

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
}
