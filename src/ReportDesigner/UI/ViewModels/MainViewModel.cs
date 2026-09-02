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

    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
    [ObservableProperty] public partial string Title { get; set; } = "ReportDesigner";

    public DesignSurfaceViewModel DesignSurface { get; }
    public ObjectTreeViewModel ObjectTree { get; }
    public PropertiesPanelViewModel PropertiesPanel { get; }

    public Bitmap? PreviewImage { get; private set; }
    public event Action? PreviewChanged;

    public MainViewModel(
        IFastReportService fastReportService,
        IPreviewService previewService,
        IFilesService filesService,
        IDialogService dialogService)
    {
        _fastReportService = fastReportService;
        _previewService = previewService;
        _filesService = filesService;
        _dialogService = dialogService;

        _fastReportService.CreateNew();

        DesignSurface = new DesignSurfaceViewModel(_fastReportService);
        DesignSurface.DocumentChanged += OnDesignSurfaceDocumentChanged;
        ObjectTree = new ObjectTreeViewModel(_fastReportService, DesignSurface);
        PropertiesPanel = new PropertiesPanelViewModel(_fastReportService, DesignSurface, _filesService);

        RefreshPreview();
        UpdateTitle();
    }

    private void OnDesignSurfaceDocumentChanged()
    {
        RefreshPreview();
        UpdateTitle();
    }

    public void RefreshPreview()
    {
        PreviewImage = _previewService.RenderPreview(_fastReportService.CurrentReport);
        PreviewChanged?.Invoke();
    }

    private void UpdateTitle()
    {
        var fileName = _fastReportService.CurrentFilePath is { } path
            ? Path.GetFileName(path)
            : "Новый документ";
        var dirtyMark = _fastReportService.IsDirty ? "*" : "";
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

        _fastReportService.Load(path);
        DesignSurface.Reset(); // сам обновит превью и заголовок через DocumentChanged
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
        UpdateTitle();
    }

    private async Task<bool> SaveInternalAsync()
    {
        var path = _fastReportService.CurrentFilePath
            ?? await _filesService.PickSaveReportPathAsync("Отчёт.frx");
        if (path is null) return false;

        _fastReportService.Save(path);
        UpdateTitle();
        return true;
    }

    /// <summary>Вызывается при закрытии окна — та же логика подтверждения, что и для New/Open.</summary>
    public Task<bool> TryPrepareForCloseAsync() => ConfirmDiscardIfDirtyAsync();
}
