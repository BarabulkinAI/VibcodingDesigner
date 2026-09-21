using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Serializer;
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

    private const int LayoutVersion = 1;

    private sealed record LayoutEnvelope(int Version, bool PreviewVisible, string Layout);

    /// <summary>Сериализует раскладку (размеры, порядок и расположение панелей, плавающие окна).
    /// Скрытая панель превью на время сериализации возвращается в раскладку — иначе при
    /// следующем запуске её негде было бы восстановить; видимость хранится отдельным флагом.</summary>
    public string SerializeLayout(IRootDock root, bool previewVisible)
    {
        if (!previewVisible) SetPreviewVisible(true);
        try
        {
            var layout = new DockSerializer(typeof(List<>)).Serialize<IDock>(root);
            return JsonSerializer.Serialize(new LayoutEnvelope(LayoutVersion, previewVisible, layout));
        }
        finally
        {
            if (!previewVisible) SetPreviewVisible(false);
        }
    }

    /// <summary>Восстанавливает раскладку из <see cref="SerializeLayout"/>. Возвращает null, если
    /// данные повреждены, от другой версии или в них не хватает какой-либо из панелей — тогда
    /// вызывающий использует <see cref="CreateLayout"/>. К результату ещё нужно применить
    /// <see cref="InitLayout"/>.</summary>
    public IRootDock? RestoreLayout(string json, out bool previewVisible)
    {
        previewVisible = true;
        try
        {
            var envelope = JsonSerializer.Deserialize<LayoutEnvelope>(json);
            if (envelope is not { Version: LayoutVersion }) return null;

            if (new DockSerializer(typeof(List<>)).Deserialize<IDock>(envelope.Layout) is not IRootDock root)
                return null;

            var all = Enumerate(root).ToList();
            var objectTree = all.OfType<ObjectTreeTool>().SingleOrDefault();
            var canvas = all.OfType<DesignCanvasDocument>().SingleOrDefault();
            var properties = all.OfType<PropertiesTool>().SingleOrDefault();
            var preview = all.OfType<PreviewTool>().SingleOrDefault();
            var dataSources = all.OfType<DataSourcesTool>().SingleOrDefault();
            if (objectTree is null || canvas is null || properties is null || preview is null || dataSources is null)
                return null;

            objectTree.ViewModel = _mainViewModel.ObjectTree;
            canvas.ViewModel = _mainViewModel.DesignSurface;
            properties.ViewModel = _mainViewModel.PropertiesPanel;
            preview.ViewModel = _mainViewModel;
            dataSources.ViewModel = _mainViewModel.DataSources;

            // Owner до InitLayout может быть не восстановлен десериализатором — ищем владельца сами
            var previewDock = all.OfType<IToolDock>().FirstOrDefault(d => d.VisibleDockables?.Contains(preview) == true);
            if (previewDock is null) return null;
            PreviewTool = preview;
            PreviewToolDock = previewDock;

            previewVisible = envelope.PreviewVisible;
            return root;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException
                                       or Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Обходит все панели раскладки, включая скрытые, закреплённые и плавающие окна.</summary>
    private static IEnumerable<IDockable> Enumerate(IDockable root)
    {
        var seen = new HashSet<IDockable>();
        var stack = new Stack<IDockable>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;
            yield return current;

            var children = new List<IDockable>();
            if (current is IDock dock && dock.VisibleDockables is { } visible) children.AddRange(visible);
            if (current is IRootDock rootDock)
            {
                foreach (var list in new[]
                         {
                             rootDock.HiddenDockables, rootDock.LeftPinnedDockables, rootDock.RightPinnedDockables,
                             rootDock.TopPinnedDockables, rootDock.BottomPinnedDockables,
                         })
                {
                    if (list is not null) children.AddRange(list);
                }

                if (rootDock.Windows is { } windows)
                {
                    children.AddRange(windows.Select(w => w.Layout).OfType<IDockable>());
                }
            }

            foreach (var child in children) stack.Push(child);
        }
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
