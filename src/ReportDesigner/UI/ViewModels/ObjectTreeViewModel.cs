using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

/// <summary>Лёгкий узел объекта для биндинга дерева (не модель домена — только для UI).</summary>
public sealed class ObjectTreeObjectNode
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Лёгкий узел полосы для биндинга дерева.</summary>
public sealed class ObjectTreeBandNode
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<ObjectTreeObjectNode> Objects { get; init; }
}

/// <summary>
/// Дерево «полосы → объекты» + управление z-order. Мутирует документ напрямую через
/// <see cref="IFastReportService"/> (а не через <see cref="DesignSurfaceViewModel"/>, чтобы не
/// раздувать его сеттерами не по его профилю), но после каждой мутации вызывает
/// <see cref="DesignSurfaceViewModel.CommitChange"/>, чтобы канвас и превью остались в синхроне.
/// </summary>
public partial class ObjectTreeViewModel : ViewModelBase
{
    private readonly IFastReportService _service;
    private readonly DesignSurfaceViewModel _designSurface;
    private bool _syncingSelection;

    [ObservableProperty] public partial IReadOnlyList<ObjectTreeBandNode> Bands { get; set; } = Array.Empty<ObjectTreeBandNode>();
    [ObservableProperty] public partial ObjectTreeObjectNode? SelectedNode { get; set; }

    public ObjectTreeViewModel(IFastReportService service, DesignSurfaceViewModel designSurface)
    {
        _service = service;
        _designSurface = designSurface;

        _designSurface.DocumentChanged += RebuildTree;
        _designSurface.PropertyChanged += OnDesignSurfacePropertyChanged;

        RebuildTree();
    }

    private void OnDesignSurfacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignSurfaceViewModel.SelectedObjectName))
            SyncSelectedNodeFromDesignSurface();
    }

    partial void OnSelectedNodeChanged(ObjectTreeObjectNode? value)
    {
        if (_syncingSelection) return;
        if (_designSurface.SelectedObjectName != value?.Name)
            _designSurface.SelectedObjectName = value?.Name;
    }

    private void SyncSelectedNodeFromDesignSurface()
    {
        if (SelectedNode?.Name == _designSurface.SelectedObjectName) return;

        _syncingSelection = true;
        SelectedNode = _designSurface.SelectedObjectName is { } name
            ? Bands.SelectMany(b => b.Objects).FirstOrDefault(o => o.Name == name)
            : null;
        _syncingSelection = false;
    }

    private void RebuildTree()
    {
        var snapshot = _designSurface.Snapshot;
        var page = snapshot.Pages.Count > 0 ? snapshot.Pages[0] : null;

        Bands = page is null
            ? Array.Empty<ObjectTreeBandNode>()
            : page.Bands.Select(b => new ObjectTreeBandNode
            {
                Name = b.Name,
                DisplayName = $"{BandKindLabel(b.Kind)} ({b.Name})",
                Objects = b.Objects.Select(o => new ObjectTreeObjectNode
                {
                    Name = o.Name,
                    DisplayName = $"{o.Name} ({ObjectTypeLabel(o.Type)})",
                }).ToList(),
            }).ToList();

        SyncSelectedNodeFromDesignSurface();
    }

    private static string BandKindLabel(BandKind kind) => kind switch
    {
        BandKind.ReportTitle => "Заголовок отчёта",
        BandKind.PageHeader => "Верхний колонтитул",
        BandKind.ColumnHeader => "Заголовок колонки",
        BandKind.GroupHeader => "Заголовок группы",
        BandKind.Data => "Данные",
        BandKind.GroupFooter => "Итог группы",
        BandKind.ColumnFooter => "Итог колонки",
        BandKind.PageFooter => "Нижний колонтитул",
        BandKind.ReportSummary => "Итог отчёта",
        BandKind.Overlay => "Оверлей",
        BandKind.Child => "Дочерняя полоса",
        _ => kind.ToString(),
    };

    private static string ObjectTypeLabel(DesignObjectType type) => type switch
    {
        DesignObjectType.Text => "Текст",
        DesignObjectType.Line => "Линия",
        DesignObjectType.Shape => "Фигура",
        DesignObjectType.Picture => "Картинка",
        _ => type.ToString(),
    };

    [RelayCommand]
    private void BringToFront()
    {
        if (SelectedNode is not { } node) return;
        _service.BringToFront(node.Name);
        _designSurface.CommitChange();
    }

    [RelayCommand]
    private void SendToBack()
    {
        if (SelectedNode is not { } node) return;
        _service.SendToBack(node.Name);
        _designSurface.CommitChange();
    }

    [RelayCommand]
    private void MoveForward()
    {
        if (SelectedNode is not { } node) return;
        _service.MoveForward(node.Name);
        _designSurface.CommitChange();
    }

    [RelayCommand]
    private void MoveBackward()
    {
        if (SelectedNode is not { } node) return;
        _service.MoveBackward(node.Name);
        _designSurface.CommitChange();
    }
}
