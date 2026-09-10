using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFastReportService _fastReportService;
    private readonly IPreviewService _previewService;
    private readonly IFilesService _filesService;
    private readonly IDialogService _dialogService;
    private readonly IExportService _exportService;
    private readonly IRecentFilesService _recentFilesService;
    
    [ObservableProperty] public partial string Title { get; set; } = "ReportDesigner";
    [ObservableProperty] public partial bool IsDocumentDirty { get; set; }
    [ObservableProperty] public partial IReadOnlyList<string> RecentFiles { get; set; } = Array.Empty<string>();
    /// <summary>Непусто, когда Report.Prepare() последний раз упал (например, [Field]-выражение
    /// ссылается на источник данных, которого больше нет — источники не сохраняются в .frx,
    /// см. docs/ARCHITECTURE.md). Показывается вместо/поверх превью, не роняя приложение.</summary>
    [ObservableProperty] public partial string? PreviewErrorMessage { get; set; }
    /// <summary>Видимость панели превью (меню «Вид» → «Панель превью») — панель забирает
    /// заметную часть ширины окна, при работе с деревом/канвасом её удобно временно скрывать.</summary>
    [ObservableProperty] public partial bool IsPreviewVisible { get; set; } = true;

    public DesignSurfaceViewModel DesignSurface { get; }
    public ObjectTreeViewModel ObjectTree { get; }
    public PropertiesPanelViewModel PropertiesPanel { get; }
    public DataSourcesViewModel DataSources { get; }

    public Bitmap? PreviewImage { get; private set; }
    public event Action? PreviewChanged;

    public MainViewModel(
        IFastReportService fastReportService,
        IPreviewService previewService,
        IFilesService filesService,
        IDialogService dialogService,
        IExportService exportService,
        IRecentFilesService recentFilesService)
    {
        _fastReportService = fastReportService;
        _previewService = previewService;
        _filesService = filesService;
        _dialogService = dialogService;
        _exportService = exportService;
        _recentFilesService = recentFilesService;

        _fastReportService.CreateNew();

        DesignSurface = new DesignSurfaceViewModel(_fastReportService);
        DesignSurface.DocumentChanged += OnDesignSurfaceDocumentChanged;
        ObjectTree = new ObjectTreeViewModel(_fastReportService, DesignSurface);
        PropertiesPanel = new PropertiesPanelViewModel(_fastReportService, DesignSurface, _filesService);
        DataSources = new DataSourcesViewModel(_fastReportService, DesignSurface);

        RefreshPreview();
        UpdateTitle();
        RefreshRecentFiles();
    }

    private void OnDesignSurfaceDocumentChanged()
    {
        RefreshPreview();
        UpdateTitle();
    }

    public void RefreshPreview()
    {
        try
        {
            PreviewImage = _previewService.RenderPreview(_fastReportService.CurrentReport);
            PreviewErrorMessage = null;
        }
        catch (Exception ex)
        {
            // Report.Prepare() может упасть на невалидном документе (типичный случай — [Field]
            // ссылается на источник данных, отвязанный при повторном открытии файла, см.
            // известные ограничения источников данных в docs/ARCHITECTURE.md). Это происходит
            // автоматически при каждом изменении документа — падать всем приложением из-за
            // такого состояния недопустимо, показываем сообщение вместо картинки.
            PreviewImage = null;
            PreviewErrorMessage = $"Не удалось подготовить превью: {ex.Message}";
        }
        PreviewChanged?.Invoke();
    }

    private void UpdateTitle()
    {
        var fileName = _fastReportService.CurrentFilePath is { } path
            ? Path.GetFileName(path)
            : "Новый документ";
        IsDocumentDirty = _fastReportService.IsDirty;
        var dirtyMark = IsDocumentDirty ? "*" : "";
        Title = $"{dirtyMark}{fileName} — ReportDesigner";
    }

    /// <summary>
    /// Если в документе есть несохранённые изменения — спрашивает пользователя, что делать.
    /// Возвращает true, если можно продолжать операцию (New/Open/Close), false — если её
    /// нужно отменить (пользователь нажал «Отмена», либо сохранение не удалось/было отменено).
    /// </summary>
    private async Task<bool> ConfirmDiscardIfDirtyAsync()
    {
        if (!_fastReportService.IsDirty) return true;

        var documentName = _fastReportService.CurrentFilePath is { } path
            ? Path.GetFileName(path)
            : "Новый документ";

        var result = await _dialogService.ConfirmDiscardChangesAsync(documentName);
        return result switch
        {
            DiscardChangesResult.Discard => true,
            DiscardChangesResult.Save => await SaveInternalAsync(),
            _ => false,
        };
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync()) return;

        _fastReportService.CreateNew();
        DesignSurface.Reset(); // сам обновит превью и заголовок через DocumentChanged
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync()) return;

        var path = await _filesService.PickOpenReportPathAsync();
        if (path is null) return;

        OpenPath(path);
    }

    /// <summary>Открывает пункт из меню «Недавние файлы» — путь уже известен, диалог выбора не нужен.</summary>
    [RelayCommand]
    private async Task OpenRecentAsync(string path)
    {
        if (!await ConfirmDiscardIfDirtyAsync()) return;

        OpenPath(path);
    }

    /// <summary>Открывает произвольный .frx как шаблон: содержимое загружается, но документ
    /// остаётся «Новым» (без пути) — как после CreateNew(). Не попадает в недавние файлы, т.к.
    /// это не документ пользователя, а исходник для нового.</summary>
    [RelayCommand]
    private async Task NewFromTemplateAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync()) return;

        var path = await _filesService.PickOpenReportPathAsync();
        if (path is null) return;

        _fastReportService.LoadAsTemplate(path);
        DesignSurface.Reset(); // сам обновит превью и заголовок через DocumentChanged
    }

    /// <summary>Общая часть OpenAsync/OpenRecentAsync: если файл не открылся — убирает его из
    /// недавних (путь, очевидно, невалиден) и пробрасывает исключение дальше.</summary>
    private void OpenPath(string path)
    {
        try
        {
            _fastReportService.Load(path);
        }
        catch
        {
            _recentFilesService.Remove(path);
            RefreshRecentFiles();
            throw;
        }

        DesignSurface.Reset(); // сам обновит превью и заголовок через DocumentChanged
        _recentFilesService.Touch(path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private async Task PageSetupAsync()
    {
        var (currentPreset, currentLandscape) = _fastReportService.GetPageSize();
        var result = await _dialogService.ChoosePageSizeAsync(currentPreset, currentLandscape);
        if (result is not { } chosen) return;

        _fastReportService.SetPageSize(chosen.Preset, chosen.Landscape);
        DesignSurface.CommitChange(); // обновит канвас/линейки/превью и запишет точку в undo
    }

    [RelayCommand]
    private Task SaveAsync() => SaveInternalAsync();

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var suggestedName = _fastReportService.CurrentFilePath is { } path
            ? Path.GetFileName(path)
            : "Отчёт.frx";

        var newPath = await _filesService.PickSaveReportPathAsync(suggestedName);
        if (newPath is null) return;

        _fastReportService.Save(newPath);
        _recentFilesService.Touch(newPath);
        RefreshRecentFiles();
        UpdateTitle();
    }

    private async Task<bool> SaveInternalAsync()
    {
        var path = _fastReportService.CurrentFilePath
            ?? await _filesService.PickSaveReportPathAsync("Отчёт.frx");
        if (path is null) return false;

        _fastReportService.Save(path);
        _recentFilesService.Touch(path);
        RefreshRecentFiles();
        UpdateTitle();
        return true;
    }

    private void RefreshRecentFiles() => RecentFiles = _recentFilesService.GetRecent();

    [RelayCommand]
    private async Task ExportPngAsync()
    {
        var path = await _filesService.PickExportPngPathAsync(SuggestedExportFileName("png"));
        if (path is null) return;

        TryPrepareAndRun(() => _exportService.ExportPng(_fastReportService.CurrentReport, path));
    }

    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        var path = await _filesService.PickExportHtmlPathAsync(SuggestedExportFileName("html"));
        if (path is null) return;

        TryPrepareAndRun(() => _exportService.ExportHtml(_fastReportService.CurrentReport, path));
    }

    /// <summary>Экспорт вызывает Report.Prepare() внутри так же, как превью — тот же класс
    /// сбоев (см. RefreshPreview) может произойти и здесь. В отсутствие сервиса диалогов ошибок
    /// просто не даём исключению уронить приложение; причина уже видна в статус-поле превью.</summary>
    private void TryPrepareAndRun(Action action)
    {
        try
        {
            action();
            PreviewErrorMessage = null;
        }
        catch (Exception ex)
        {
            PreviewErrorMessage = $"Не удалось экспортировать: {ex.Message}";
        }
    }

    private string SuggestedExportFileName(string extension)
    {
        var baseName = _fastReportService.CurrentFilePath is { } path
            ? Path.GetFileNameWithoutExtension(path)
            : "Отчёт";
        return $"{baseName}.{extension}";
    }

    /// <summary>Вызывается при закрытии окна — та же логика подтверждения, что и для New/Open.</summary>
    public Task<bool> TryPrepareForCloseAsync() => ConfirmDiscardIfDirtyAsync();
}
