using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

/// <summary>
/// Строит раскладку рабочих зон (дерево объектов, канвас, свойства, превью, источники данных)
/// поверх Dock.Avalonia. Панели-обёртки (<see cref="ObjectTreeTool"/> и т.д.) хранят ссылку на
/// уже существующую, не изменённую ради Dock ViewModel — сама Dock-интеграция не трогает логику
/// этих VM, только оборачивает их для DockControl.
/// </summary>
public sealed class MainDockFactory : Factory
{
    private readonly MainViewModel _mainViewModel;

    /// <summary>Тул-док, владеющий панелью превью — нужен, чтобы «Вид → Панель превью»
    /// (<see cref="MainViewModel.IsPreviewVisible"/>) мог добавлять/убирать панель напрямую,
    /// не опираясь на встроенный крестик закрытия Dock (см. <see cref="PreviewTool"/>).</summary>
    public IToolDock? PreviewToolDock { get; private set; }
    public PreviewTool? PreviewTool { get; private set; }

    public MainDockFactory(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public override IRootDock CreateLayout()
    {
        var objectTreeTool = new ObjectTreeTool(_mainViewModel.ObjectTree);
        var canvasDocument = new DesignCanvasDocument(_mainViewModel.DesignSurface);
        var propertiesTool = new PropertiesTool(_mainViewModel.PropertiesPanel);
        var previewTool = new PreviewTool(_mainViewModel);
        var dataSourcesTool = new DataSourcesTool(_mainViewModel.DataSources);

        var objectTreeDock = new ToolDock
        {
            ActiveDockable = objectTreeTool,
            VisibleDockables = CreateList<IDockable>(objectTreeTool),
            Alignment = Alignment.Left,
        };

        var canvasDock = new DocumentDock
        {
            IsCollapsable = false,
            ActiveDockable = canvasDocument,
            VisibleDockables = CreateList<IDockable>(canvasDocument),
            CanCreateDocument = false,
        };

        var propertiesDock = new ToolDock
        {
            ActiveDockable = propertiesTool,
            VisibleDockables = CreateList<IDockable>(propertiesTool),
            Alignment = Alignment.Right,
        };

        var previewDock = new ToolDock
        {
            ActiveDockable = previewTool,
            VisibleDockables = CreateList<IDockable>(previewTool),
            Alignment = Alignment.Right,
        };

        var topLayout = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>
            (
                objectTreeDock,
                new ProportionalDockSplitter(),
                canvasDock,
                new ProportionalDockSplitter(),
                propertiesDock,
                new ProportionalDockSplitter(),
                previewDock
            ),
        };
        objectTreeDock.Proportion = 0.18;
        canvasDock.Proportion = 0.42;
        propertiesDock.Proportion = 0.15;
        previewDock.Proportion = 0.25;

        var dataSourcesDock = new ToolDock
        {
            ActiveDockable = dataSourcesTool,
            VisibleDockables = CreateList<IDockable>(dataSourcesTool),
            Alignment = Alignment.Bottom,
        };

        var mainLayout = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>
            (
                topLayout,
                new ProportionalDockSplitter(),
                dataSourcesDock
            ),
        };
        topLayout.Proportion = 0.78;
        dataSourcesDock.Proportion = 0.22;

        var rootDock = CreateRootDock();
        rootDock.IsCollapsable = false;
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.LeftPinnedDockables = CreateList<IDockable>();
        rootDock.RightPinnedDockables = CreateList<IDockable>();
        rootDock.TopPinnedDockables = CreateList<IDockable>();
        rootDock.BottomPinnedDockables = CreateList<IDockable>();
        rootDock.PinnedDock = null;

        PreviewToolDock = previewDock;
        PreviewTool = previewTool;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow(),
        };

        base.InitLayout(layout);
    }

    /// <summary>Показывает/скрывает панель превью — вызывается из
    /// <see cref="MainViewModel.IsPreviewVisible"/>. Убирает/возвращает панель в её
    /// <see cref="PreviewToolDock"/> напрямую, а не через Dock-команду закрытия, чтобы чекбокс
    /// «Вид → Панель превью» оставался единственным источником истины для видимости.</summary>
    public void SetPreviewVisible(bool visible)
    {
        if (PreviewToolDock?.VisibleDockables is not { } dockables || PreviewTool is not { } tool) return;

        var isVisible = dockables.Contains(tool);
        if (visible && !isVisible)
        {
            dockables.Add(tool);
            PreviewToolDock.ActiveDockable = tool;
        }
        else if (!visible && isVisible)
        {
            dockables.Remove(tool);
        }
    }
}
