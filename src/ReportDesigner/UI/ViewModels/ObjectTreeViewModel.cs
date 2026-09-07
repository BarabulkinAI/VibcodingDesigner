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
    public required BandKind Kind { get; init; }
    public required float HeightPx { get; init; }
    public string? DataSourceName { get; init; }
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
    /// <summary>Полосы, которых на странице может быть только одна — «Добавить» для них
    /// заблокировано, пока такая полоса уже присутствует в снимке.</summary>
    private static readonly HashSet<BandKind> SingletonBandKinds = new()
    {
        BandKind.ReportTitle, BandKind.PageHeader, BandKind.ColumnHeader,
        BandKind.ColumnFooter, BandKind.PageFooter, BandKind.ReportSummary, BandKind.Overlay,
    };

    private const string NoDataSourceOption = "(нет)";

    private readonly IFastReportService _service;
    private readonly DesignSurfaceViewModel _designSurface;
    private bool _syncingSelection;
    private bool _isRefreshingBand;
    /// <summary>Новое имя переименованной полосы — используется единственный следующий
    /// <see cref="RebuildTree"/>, чтобы восстановить выделение под новым именем (иначе оно
    /// потерялось бы, так как узел ищется по имени, а старое уже не существует).</summary>
    private string? _pendingBandSelectionName;

    [ObservableProperty] public partial IReadOnlyList<ObjectTreeBandNode> Bands { get; set; } = Array.Empty<ObjectTreeBandNode>();
    [ObservableProperty] public partial ObjectTreeObjectNode? SelectedNode { get; set; }

    [ObservableProperty] public partial ObjectTreeBandNode? SelectedBandNode { get; set; }
    [ObservableProperty] public partial bool IsBandSelected { get; set; }
    [ObservableProperty] public partial string BandNameEdit { get; set; } = "";
    [ObservableProperty] public partial double BandHeightCm { get; set; }
    [ObservableProperty] public partial bool IsDataBandSelected { get; set; }
    [ObservableProperty] public partial string SelectedBandDataSource { get; set; } = NoDataSourceOption;
    [ObservableProperty] public partial IReadOnlyList<string> AvailableDataSourceOptions { get; set; } = new[] { NoDataSourceOption };

    [ObservableProperty] public partial BandKind SelectedKindToAdd { get; set; } = BandKind.Data;
    [ObservableProperty] public partial bool CanAddSelectedKind { get; set; } = true;

    /// <summary>GroupFooter и Child исключены: в FastReport это не самостоятельные полосы
    /// страницы, а вложенные (привязываются к родительской полосе) — см.
    /// <see cref="IFastReportService.AddBand"/>. Добавлять их через этот комбобокс нельзя,
    /// пока в дизайнере нет UI для выбора родительской полосы.</summary>
    public IReadOnlyList<BandKind> BandKindOptions { get; } = Enum.GetValues<BandKind>()
        .Where(k => k is not (BandKind.GroupFooter or BandKind.Child))
        .ToList();

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

    partial void OnSelectedBandNodeChanged(ObjectTreeBandNode? value) => RefreshBandFields();

    partial void OnSelectedKindToAddChanged(BandKind value) => RefreshCanAddSelectedKind();

    private void SyncSelectedNodeFromDesignSurface()
    {
        if (SelectedNode?.Name == _designSurface.SelectedObjectName) return;

        _syncingSelection = true;
        SelectedNode = _designSurface.SelectedObjectName is { } name
            ? Bands.SelectMany(b => b.Objects).FirstOrDefault(o => o.Name == name)
            : null;
        _syncingSelection = false;
    }

    private void RefreshBandFields()
    {
        _isRefreshingBand = true;
        try
        {
            IsBandSelected = SelectedBandNode is not null;
            BandNameEdit = SelectedBandNode?.Name ?? "";
            IsDataBandSelected = SelectedBandNode?.Kind == BandKind.Data;

            if (SelectedBandNode is { } node)
            {
                var current = Bands.First(b => b.Name == node.Name);
                BandHeightCm = Math.Round(UnitConverter.PxToCm(current.HeightPx), 2);
                SelectedBandDataSource = current.DataSourceName ?? NoDataSourceOption;
            }
            else
            {
                BandHeightCm = 0;
                SelectedBandDataSource = NoDataSourceOption;
            }
        }
        finally
        {
            _isRefreshingBand = false;
        }
    }

    private void RefreshCanAddSelectedKind() =>
        CanAddSelectedKind = !SingletonBandKinds.Contains(SelectedKindToAdd)
            || Bands.All(b => b.Kind != SelectedKindToAdd);

    private void RebuildTree()
    {
        var snapshot = _designSurface.Snapshot;
        var page = snapshot.Pages.Count > 0 ? snapshot.Pages[0] : null;

        Bands = page is null
            ? Array.Empty<ObjectTreeBandNode>()
            : page.Bands.Select(b => new ObjectTreeBandNode
            {
                Name = b.Name,
                Kind = b.Kind,
                HeightPx = b.Height,
                DataSourceName = b.DataSourceName,
                DisplayName = $"{BandKindLabel(b.Kind)} ({b.Name})",
                Objects = b.Objects.Select(o => new ObjectTreeObjectNode
                {
                    Name = o.Name,
                    DisplayName = $"{o.Name} ({ObjectTypeLabel(o.Type)})",
                }).ToList(),
            }).ToList();

        AvailableDataSourceOptions = new[] { NoDataSourceOption }.Concat(_service.GetDataSourceNames()).ToList();

        SyncSelectedNodeFromDesignSurface();

        var lookupName = _pendingBandSelectionName ?? SelectedBandNode?.Name;
        _pendingBandSelectionName = null;
        SelectedBandNode = lookupName is { } name
            ? Bands.FirstOrDefault(b => b.Name == name)
            : null;
        RefreshCanAddSelectedKind();
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

    // ------------------------------------------------------------------
    // Управление полосами
    // ------------------------------------------------------------------

    [RelayCommand]
    private void AddBand()
    {
        _service.AddBand(SelectedKindToAdd, 2f);
        _designSurface.CommitChange();
    }

    [RelayCommand]
    private void RemoveSelectedBand()
    {
        if (SelectedBandNode is not { } node) return;
        _service.RemoveBand(node.Name);
        SelectedBandNode = null;
        _designSurface.CommitChange();
    }

    partial void OnBandNameEditChanged(string value)
    {
        if (_isRefreshingBand || SelectedBandNode is not { } node) return;
        if (string.IsNullOrWhiteSpace(value) || value == node.Name) return;
        _service.RenameBand(node.Name, value);
        _pendingBandSelectionName = value;
        _designSurface.CommitChange();
    }

    partial void OnBandHeightCmChanged(double value)
    {
        if (_isRefreshingBand || SelectedBandNode is not { } node) return;
        _service.SetBandHeight(node.Name, (float)value);
        _designSurface.CommitChange();
    }

    /// <summary>
    /// Кроме обычного guard-а по _isRefreshingBand, здесь ещё и проверка Kind == Data: Avalonia
    /// сбрасывает SelectedItem комбобокса при смене его ItemsSource (AvailableDataSourceOptions
    /// пересобирается заново на каждый RebuildTree — новый экземпляр списка) и пишет это обратно
    /// в SelectedBandDataSource через биндинг ДАЖЕ когда комбобокс скрыт (IsVisible=False не
    /// отключает биндинг) — вне контролируемого окна _isRefreshingBand в RefreshBandFields().
    /// Без этой проверки такой сброс на невыделенной полосе данных (например, PageHeader) валил
    /// AssignBandDataSource с InvalidOperationException — воспроизведено пользователем вручную.
    /// </summary>
    partial void OnSelectedBandDataSourceChanged(string value)
    {
        if (_isRefreshingBand || SelectedBandNode is not { Kind: BandKind.Data } node) return;
        _service.AssignBandDataSource(node.Name, value == NoDataSourceOption ? null : value);
        _designSurface.CommitChange();
    }
}
