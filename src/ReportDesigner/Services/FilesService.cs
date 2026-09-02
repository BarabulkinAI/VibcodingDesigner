using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ReportDesigner.Services;

public class FilesService : IFilesService
{
    private readonly IHostWindowProvider _hostWindowProvider;

    public FilesService(IHostWindowProvider hostWindowProvider)
    {
        _hostWindowProvider = hostWindowProvider;
    }

    private static readonly FilePickerFileType ReportFileType = new("Отчёты FastReport (*.frx)")
    {
        Patterns = new[] { "*.frx" },
    };

    private static readonly FilePickerFileType ImageFileType = new("Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif)")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" },
    };

    public async Task<string?> PickOpenReportPathAsync()
    {
        var storageProvider = GetStorageProvider();
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть отчёт",
            AllowMultiple = false,
            FileTypeFilter = new[] { ReportFileType },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveReportPathAsync(string? suggestedFileName)
    {
        var storageProvider = GetStorageProvider();
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить отчёт как",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "frx",
            FileTypeChoices = new[] { ReportFileType },
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickImagePathAsync()
    {
        var storageProvider = GetStorageProvider();
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбрать изображение",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private IStorageProvider GetStorageProvider()
    {
        var topLevel = TopLevel.GetTopLevel(_hostWindowProvider.MainWindow)
            ?? throw new InvalidOperationException("Не удалось получить TopLevel главного окна.");
        return topLevel.StorageProvider;
    }
}
