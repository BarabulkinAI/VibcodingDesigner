using System.Linq;
using Avalonia.Media.Imaging;
using FastReport;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

/// <summary>
/// MainViewModel раньше не имел тестов вообще (New/Open/Save/SaveAs, диалог подтверждения
/// потери изменений, обновление заголовка) — закрывается здесь же, где MainViewModel
/// расширяется командами экспорта (этап 4, фаза 1).
/// </summary>
public class MainViewModelTests
{
    private sealed class FakePreviewService : IPreviewService
    {
        public int RenderCount { get; private set; }
        public Bitmap RenderPreview(Report report)
        {
            RenderCount++;
            return null!; // MainViewModel только присваивает результат в PreviewImage, не разыменовывает
        }
    }

    private sealed class FakeFilesService : IFilesService
    {
        public string? NextOpenPath { get; set; }
        public string? NextSavePath { get; set; }
        public string? NextExportPngPath { get; set; }
        public string? NextExportHtmlPath { get; set; }
        public List<string?> SaveFileNameRequests { get; } = new();

        public Task<string?> PickOpenReportPathAsync() => Task.FromResult(NextOpenPath);

        public Task<string?> PickSaveReportPathAsync(string? suggestedFileName)
        {
            SaveFileNameRequests.Add(suggestedFileName);
            return Task.FromResult(NextSavePath);
        }

        public Task<string?> PickImagePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickExportPngPathAsync(string? suggestedFileName) => Task.FromResult(NextExportPngPath);
        public Task<string?> PickExportHtmlPathAsync(string? suggestedFileName) => Task.FromResult(NextExportHtmlPath);
    }

    private sealed class FakeDialogService : IDialogService
    {
        public DiscardChangesResult NextResult { get; set; } = DiscardChangesResult.Discard;
        public int CallCount { get; private set; }

        public Task<DiscardChangesResult> ConfirmDiscardChangesAsync(string documentDisplayName)
        {
            CallCount++;
            return Task.FromResult(NextResult);
        }
    }

    private static (FastReportService Service, FakeFilesService Files, FakeDialogService Dialog, RecentFilesService Recent, MainViewModel Main) Create()
    {
        var service = new FastReportService();
        var files = new FakeFilesService();
        var dialog = new FakeDialogService();
        var recent = new RecentFilesService(Path.Combine(Path.GetTempPath(), $"mvm_recent_{Guid.NewGuid():N}.json"));
        var main = new MainViewModel(service, new FakePreviewService(), files, dialog, new ExportService(), recent);
        return (service, files, dialog, recent, main);
    }

    private static string TempReportPath() => Path.Combine(Path.GetTempPath(), $"mvm_{Guid.NewGuid():N}.frx");

    [Fact]
    public async Task NewAsync_PromptsWhenDirty()
    {
        var (service, _, dialog, _, main) = Create();
        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        dialog.NextResult = DiscardChangesResult.Discard;

        await main.NewCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.CallCount);
        Assert.False(service.IsDirty);
        Assert.Null(service.CurrentFilePath);
    }

    [Fact]
    public async Task NewAsync_NotDirty_DoesNotPrompt()
    {
        var (_, _, dialog, _, main) = Create();

        await main.NewCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.CallCount);
    }

    [Fact]
    public async Task NewAsync_CancelledDialog_KeepsCurrentDocument()
    {
        var (service, _, dialog, _, main) = Create();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        dialog.NextResult = DiscardChangesResult.Cancel;

        await main.NewCommand.ExecuteAsync(null);

        Assert.True(service.IsDirty);
        Assert.Contains(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects), o => o.Name == name);
    }

    [Fact]
    public async Task OpenAsync_LoadsSelectedFile()
    {
        var path = TempReportPath();
        try
        {
            var setup = new FastReportService();
            setup.CreateNew();
            setup.Save(path); // готовим файл, который потом "откроем"

            var (service, files, _, recent, main) = Create();
            files.NextOpenPath = path;

            await main.OpenCommand.ExecuteAsync(null);

            Assert.Equal(path, service.CurrentFilePath);
            Assert.False(service.IsDirty);
            Assert.Equal(path, Assert.Single(recent.GetRecent()));
            Assert.Equal(path, Assert.Single(main.RecentFiles));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task OpenRecentAsync_OpensWithoutFilePicker()
    {
        var path = TempReportPath();
        try
        {
            var setup = new FastReportService();
            setup.CreateNew();
            setup.Save(path);

            var (service, files, _, _, main) = Create();
            files.NextOpenPath = null; // если бы вызвался диалог — путь остался бы неизвестен

            await main.OpenRecentCommand.ExecuteAsync(path);

            Assert.Equal(path, service.CurrentFilePath);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task OpenRecentAsync_InvalidPath_RemovesFromRecentAndThrows()
    {
        var (_, _, _, recent, main) = Create();
        var badPath = Path.Combine(Path.GetTempPath(), $"mvm_missing_{Guid.NewGuid():N}.frx");
        recent.Touch(badPath);

        await Assert.ThrowsAnyAsync<Exception>(() => main.OpenRecentCommand.ExecuteAsync(badPath));

        Assert.DoesNotContain(badPath, recent.GetRecent());
    }

    [Fact]
    public async Task NewFromTemplateAsync_LoadsContentButKeepsDocumentNew()
    {
        var path = TempReportPath();
        try
        {
            var setup = new FastReportService();
            setup.CreateNew();
            setup.AddObject(DesignObjectType.Text, 1, 1, 2, 1);
            setup.Save(path);

            var (service, files, _, recent, main) = Create();
            files.NextOpenPath = path;

            await main.NewFromTemplateCommand.ExecuteAsync(null);

            Assert.Null(service.CurrentFilePath); // документ остаётся «Новым»
            Assert.False(service.IsDirty);
            Assert.Single(service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects)); // содержимое шаблона загрузилось
            Assert.Empty(recent.GetRecent()); // шаблон не попадает в недавние файлы
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    /// <summary>
    /// Регрессия: [Field]-выражение, оставшееся в тексте после того как источник данных исчез
    /// при перезагрузке (источники не сохраняются в .frx), не компилируется FastReport-ом
    /// (CS0103: имя не существует в текущем контексте) — Report.Prepare() бросает исключение.
    /// RefreshPreview() вызывается автоматически при каждом открытии файла — такое исключение
    /// не должно ронять приложение целиком (именно так пользователь и словил краш вручную).
    /// </summary>
    [Fact]
    public async Task OpenAsync_ReportWithDanglingFieldExpression_DoesNotCrashPreview()
    {
        var path = TempReportPath();
        try
        {
            var setup = new FastReportService();
            setup.CreateNew();
            setup.SetDataSource("Источник", new[] { "Столбец1" }, new[] { new[] { "значение1" } });
            var dataBand = setup.GetSnapshot().Pages[0].Bands.Single(b => b.Kind == BandKind.Data).Name;
            setup.AssignBandDataSource(dataBand, "Источник");
            var textName = setup.AddObject(DesignObjectType.Text, 0, 0, 4, 1, dataBand);
            setup.SetText(textName, "[Источник.Столбец1]");
            setup.Save(path);

            // Реальный PreviewService (не FakePreviewService из Create()) — тест должен
            // прогнать настоящий Report.Prepare(), иначе ничего не проверяет.
            var service = new FastReportService();
            var files = new FakeFilesService { NextOpenPath = path };
            var main = new MainViewModel(service, new PreviewService(), files, new FakeDialogService(),
                new ExportService(), new RecentFilesService(Path.Combine(Path.GetTempPath(), $"mvm_recent_{Guid.NewGuid():N}.json")));

            await main.OpenCommand.ExecuteAsync(null); // не должно бросать исключение наружу

            Assert.NotNull(main.PreviewErrorMessage);
            Assert.Null(main.PreviewImage);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task OpenAsync_CancelledPicker_NoOp()
    {
        var (service, files, _, _, main) = Create();
        files.NextOpenPath = null;

        await main.OpenCommand.ExecuteAsync(null);

        Assert.Null(service.CurrentFilePath);
    }

    [Fact]
    public async Task SaveAsync_PromptsForPathWhenNew_ThenReusesIt()
    {
        var (service, files, _, recent, main) = Create();
        var path = TempReportPath();
        files.NextSavePath = path;
        try
        {
            await main.SaveCommand.ExecuteAsync(null);
            Assert.Equal(path, service.CurrentFilePath);
            Assert.Single(files.SaveFileNameRequests);
            Assert.Equal(path, Assert.Single(recent.GetRecent()));

            service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
            await main.SaveCommand.ExecuteAsync(null);

            // Путь уже известен — второй раз диалог сохранения не должен запрашиваться.
            Assert.Single(files.SaveFileNameRequests);
            Assert.False(service.IsDirty);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task SaveAsAsync_AlwaysPromptsForPath()
    {
        var (service, files, _, recent, main) = Create();
        var path = TempReportPath();
        files.NextSavePath = path;
        try
        {
            await main.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(path, service.CurrentFilePath);
            Assert.Single(files.SaveFileNameRequests);
            Assert.Equal(path, Assert.Single(recent.GetRecent()));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public void Title_ReflectsDirtyAndFileName()
    {
        var (service, _, _, _, main) = Create();

        Assert.Contains("Новый документ", main.Title);
        Assert.DoesNotContain("*", main.Title);
        Assert.False(main.IsDocumentDirty);

        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        main.DesignSurface.CommitChange(); // тем же путём, что и остальные ViewModel, обновляет заголовок

        Assert.StartsWith("*", main.Title);
        Assert.True(main.IsDocumentDirty);
    }

    [Fact]
    public async Task TryPrepareForCloseAsync_DelegatesToConfirmDiscard()
    {
        var (service, _, dialog, _, main) = Create();
        service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        dialog.NextResult = DiscardChangesResult.Discard;

        var result = await main.TryPrepareForCloseAsync();

        Assert.True(result);
        Assert.Equal(1, dialog.CallCount);
    }

    [Fact]
    public async Task ExportPngAsync_WritesFileToChosenPath()
    {
        var (_, files, _, _, main) = Create();
        var path = Path.Combine(Path.GetTempPath(), $"mvm_export_{Guid.NewGuid():N}.png");
        files.NextExportPngPath = path;
        try
        {
            await main.ExportPngCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task ExportHtmlAsync_WritesFileToChosenPath()
    {
        var (_, files, _, _, main) = Create();
        var path = Path.Combine(Path.GetTempPath(), $"mvm_export_{Guid.NewGuid():N}.html");
        files.NextExportHtmlPath = path;
        try
        {
            await main.ExportHtmlCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json"); // File.Delete не бросает, если файла нет
        }
    }

    [Fact]
    public async Task ExportPngAsync_CancelledPicker_NoOp()
    {
        var (_, files, _, _, main) = Create();
        files.NextExportPngPath = null;

        await main.ExportPngCommand.ExecuteAsync(null); // не должно бросать/падать
    }
}
