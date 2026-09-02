using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

/// <summary>
/// Панель источников данных: список именованных примерных таблиц, каждая редактируется как
/// CSV-текст (первая строка — заголовки столбцов) — простой TextBox без грид-контрола, тот же
/// принцип минимализма, что и у цветов в PropertiesPanelViewModel (hex-строка вместо пикера).
/// Мутирует документ напрямую через <see cref="IFastReportService"/> (как ObjectTreeViewModel/
/// PropertiesPanelViewModel) и синхронизирует канвас/превью через
/// <see cref="DesignSurfaceViewModel.CommitChange"/>.
///
/// Источники данных не сохраняются в .frx (см. docs/ARCHITECTURE.md, известные риски) — это
/// примерные данные для дизайна, а не подключение к реальному источнику.
/// </summary>
public partial class DataSourcesViewModel : ViewModelBase
{
    private readonly IFastReportService _service;
    private readonly DesignSurfaceViewModel _designSurface;
    private bool _isRefreshing;
    private string? _currentSourceName;

    [ObservableProperty] public partial IReadOnlyList<string> SourceNames { get; set; } = Array.Empty<string>();
    [ObservableProperty] public partial string? SelectedSourceName { get; set; }
    [ObservableProperty] public partial bool HasSelection { get; set; }
    [ObservableProperty] public partial string NameEdit { get; set; } = "";
    [ObservableProperty] public partial string CsvText { get; set; } = "";

    public DataSourcesViewModel(IFastReportService service, DesignSurfaceViewModel designSurface)
    {
        _service = service;
        _designSurface = designSurface;

        _designSurface.DocumentChanged += RebuildSources;

        RebuildSources();
    }

    partial void OnSelectedSourceNameChanged(string? value) => RefreshFromSelection();

    /// <summary>
    /// Обновляет только список имён. НЕ перечитывает CsvText/NameEdit для всё ещё выбранного
    /// источника: сервис не хранит достаточно, чтобы восстановить CSV-текст побайтово так, как
    /// его ввёл пользователь (см. <see cref="BuildCsvText"/>), а этот метод вызывается в том
    /// числе после DocumentChanged, порождённого нашим же вводом (OnCsvTextChanged →
    /// SetDataSource → CommitChange) — перечитывание на каждое нажатие клавиши переформатировало
    /// бы текст и сбрасывало курсор в начало прямо во время печати.
    /// </summary>
    private void RebuildSources()
    {
        SourceNames = _service.GetDataSourceNames();

        if (SelectedSourceName is { } name && !SourceNames.Contains(name))
            SelectedSourceName = null;
    }

    private void RefreshFromSelection()
    {
        _isRefreshing = true;
        try
        {
            _currentSourceName = SelectedSourceName;
            HasSelection = _currentSourceName is not null;

            if (_currentSourceName is not { } name)
            {
                NameEdit = "";
                CsvText = "";
                return;
            }

            NameEdit = name;
            var columns = _service.GetDataSourceColumns(name);
            CsvText = BuildCsvText(columns);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private void AddSource()
    {
        var name = EnsureUniqueName("Источник");
        _service.SetDataSource(name, new[] { "Столбец1" }, Array.Empty<IReadOnlyList<string>>());
        _designSurface.CommitChange();
        SelectedSourceName = name;
    }

    [RelayCommand]
    private void RemoveSelectedSource()
    {
        if (_currentSourceName is not { } name) return;
        _service.RemoveDataSource(name);
        SelectedSourceName = null;
        _designSurface.CommitChange();
    }

    partial void OnNameEditChanged(string value)
    {
        if (_isRefreshing || _currentSourceName is not { } name) return;
        if (string.IsNullOrWhiteSpace(value) || value == name) return;

        _service.RenameDataSource(name, value);
        _currentSourceName = value;
        SelectedSourceName = value; // до CommitChange, чтобы RebuildSources не сбросил выделение
        _designSurface.CommitChange();
    }

    partial void OnCsvTextChanged(string value)
    {
        if (_isRefreshing || _currentSourceName is not { } name) return;

        var (columns, rows) = ParseCsvText(value);
        if (columns.Count == 0) return;

        _service.SetDataSource(name, columns, rows);
        _designSurface.CommitChange();
    }

    private string EnsureUniqueName(string baseName)
    {
        if (!SourceNames.Contains(baseName)) return baseName;
        for (var i = 1; ; i++)
        {
            var candidate = $"{baseName}{i}";
            if (!SourceNames.Contains(candidate)) return candidate;
        }
    }

    private static string BuildCsvText(IReadOnlyList<string> columns)
    {
        // Строки не читаются обратно из FastReport (сервис хранит только зарегистрированные
        // данные, не отдаёт их наружу) — при повторном выборе источника показываются только
        // заголовки столбцов; пользователь дополняет строки вручную. Это сознательное
        // упрощение: полноценное хранение введённых строк потребовало бы дублировать их и в
        // сервисе, и здесь.
        return string.Join(", ", columns);
    }

    private static (List<string> Columns, List<IReadOnlyList<string>> Rows) ParseCsvText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var columns = lines.Length > 0
            ? lines[0].Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList()
            : new List<string>();

        var rows = new List<IReadOnlyList<string>>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = lines[i].Split(',').Select(c => c.Trim()).ToList();
            while (cells.Count < columns.Count) cells.Add("");
            rows.Add(cells.Take(columns.Count).ToList());
        }

        return (columns, rows);
    }
}
