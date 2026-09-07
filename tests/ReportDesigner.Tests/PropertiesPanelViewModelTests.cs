using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

public class PropertiesPanelViewModelTests
{
    private sealed class FakeFilesService : IFilesService
    {
        public string? NextImagePath { get; set; }
        public Task<string?> PickOpenReportPathAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickSaveReportPathAsync(string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<string?> PickImagePathAsync() => Task.FromResult(NextImagePath);
        public Task<string?> PickExportPngPathAsync(string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<string?> PickExportHtmlPathAsync(string? suggestedFileName) => Task.FromResult<string?>(null);
    }

    private static (FastReportService Service, DesignSurfaceViewModel Surface, PropertiesPanelViewModel Panel)
        Create()
    {
        var service = new FastReportService();
        service.CreateNew();
        var surface = new DesignSurfaceViewModel(service);
        var panel = new PropertiesPanelViewModel(service, surface, new FakeFilesService());
        return (service, surface, panel);
    }

    [Fact]
    public void NoSelection_HasSelectionFalse()
    {
        var (_, _, panel) = Create();

        Assert.False(panel.HasSelection);
    }

    [Fact]
    public void SelectingTextObject_PopulatesFieldsFromObject()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Text, 1f, 2f, 4f, 1.5f);
        service.SetText(name, "Привет");
        service.SetFont(name, "Calibri", 14f, bold: true, italic: false);
        surface.CommitChange();

        surface.SelectedObjectName = name;

        Assert.True(panel.HasSelection);
        Assert.True(panel.IsTextObject);
        Assert.False(panel.IsShapeObject);
        Assert.Equal(1f, panel.LeftCm, 2);
        Assert.Equal(2f, panel.TopCm, 2);
        Assert.Equal(4f, panel.WidthCm, 2);
        Assert.Equal(1.5f, panel.HeightCm, 2);
        Assert.Equal("Привет", panel.Text);
        Assert.Equal("Calibri", panel.FontName);
        Assert.Equal(14, panel.FontSize, 2);
        Assert.True(panel.FontBold);
        Assert.False(panel.FontItalic);
    }

    /// <summary>
    /// Регрессия на класс бага, найденный на этапе 1 (CommitDrag/CommitResize): заполнение
    /// полей при смене выделения само вызывает генерируемые OnXxxChanged для каждого поля.
    /// Без guard'а (_isRefreshing) это вызвало бы сеттеры сервиса и выставило IsDirty на
    /// простом клике по объекту в дереве/на канвасе — что явно нежелательно.
    /// </summary>
    [Fact]
    public void SelectingObjects_NeverDirtiesDocument()
    {
        var service = new FastReportService();
        service.CreateNew();
        var textName = service.AddObject(DesignObjectType.Text, 1, 1, 4, 1);
        var shapeName = service.AddObject(DesignObjectType.Shape, 5, 1, 3, 2);
        service.MarkSaved("fixture.frx"); // сбрасываем dirty после подготовки фикстуры

        var surface = new DesignSurfaceViewModel(service);
        var panel = new PropertiesPanelViewModel(service, surface, new FakeFilesService());
        Assert.False(service.IsDirty);

        surface.SelectedObjectName = textName;
        Assert.False(service.IsDirty);

        surface.SelectedObjectName = shapeName;
        Assert.False(service.IsDirty);

        surface.SelectedObjectName = null;
        Assert.False(service.IsDirty);
        Assert.False(panel.HasSelection);
    }

    [Fact]
    public void ChangingLeftCm_MovesObjectAndCommitsChange()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 1, 1, 2, 2);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        var documentChangedRaised = false;
        surface.DocumentChanged += () => documentChangedRaised = true;

        panel.LeftCm = 3.5;

        var bounds = service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name).Bounds;
        Assert.Equal(UnitConverter.CmToPx(3.5f), bounds.X, 1);
        Assert.True(documentChangedRaised);
    }

    [Fact]
    public void ChangingWidthAndHeight_ResizesObject()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 1, 1, 2, 2);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        panel.WidthCm = 5;
        panel.HeightCm = 3;

        var bounds = service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name).Bounds;
        Assert.Equal(UnitConverter.CmToPx(5f), bounds.Width, 1);
        Assert.Equal(UnitConverter.CmToPx(3f), bounds.Height, 1);
    }

    [Fact]
    public void ChangingText_UpdatesServiceText()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        panel.Text = "Новый текст";

        var obj = service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name);
        Assert.Equal("Новый текст", obj.Text);
    }

    [Fact]
    public void ChangingFillColorHex_UpdatesShapeFillColor()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        panel.FillColorHex = "#3366FF";

        var obj = service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name);
        Assert.Equal(0x33, obj.FillColor.R);
        Assert.Equal(0x66, obj.FillColor.G);
        Assert.Equal(0xFF, obj.FillColor.B);
    }

    [Fact]
    public void ChangingFillColorHex_WithInvalidValue_DoesNotThrowOrDirtyDocument()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        service.MarkSaved("fixture.frx");
        surface.SelectedObjectName = name;

        panel.FillColorHex = "не цвет"; // пользователь ещё печатает — не должно падать

        Assert.False(service.IsDirty);
    }

    [Fact]
    public void ChangingVisible_UpdatesServiceVisibility()
    {
        var (service, surface, panel) = Create();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        panel.Visible = false;

        var obj = service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name);
        Assert.False(obj.Visible);
    }

    [Fact]
    public async Task PickImageCommand_SetsImageAndCommitsChange()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Picture, 0, 0, 3, 3);
        var surface = new DesignSurfaceViewModel(service);
        var filesService = new FakeFilesService { NextImagePath = CreateTempPngFile() };
        var panel = new PropertiesPanelViewModel(service, surface, filesService);
        surface.CommitChange();
        surface.SelectedObjectName = name;

        var documentChangedRaised = false;
        surface.DocumentChanged += () => documentChangedRaised = true;

        try
        {
            await panel.PickImageCommand.ExecuteAsync(null);

            Assert.True(panel.HasImage);
            Assert.True(documentChangedRaised);
        }
        finally
        {
            File.Delete(filesService.NextImagePath!);
        }
    }

    [Fact]
    public void ClearImageCommand_ResetsHasImage()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Picture, 0, 0, 3, 3);
        var surface = new DesignSurfaceViewModel(service);
        var imagePath = CreateTempPngFile();
        var panel = new PropertiesPanelViewModel(service, surface, new FakeFilesService());
        surface.CommitChange();
        surface.SelectedObjectName = name;
        service.SetImage(name, imagePath);
        surface.SelectedObjectName = null;
        surface.SelectedObjectName = name; // перечитать HasImage после SetImage

        try
        {
            panel.ClearImageCommand.Execute(null);

            Assert.False(panel.HasImage);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string CreateTempPngFile()
    {
        var path = Path.GetTempFileName() + ".png";
        using var bitmap = new System.Drawing.Bitmap(4, 4);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public void SwitchingSelectionBetweenTypes_UpdatesTypeFlagsAndDoesNotLeakFields()
    {
        var (service, surface, panel) = Create();
        var textName = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);
        service.SetText(textName, "Текстовый объект");
        var shapeName = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        surface.CommitChange();

        surface.SelectedObjectName = textName;
        Assert.True(panel.IsTextObject);
        Assert.Equal("Текстовый объект", panel.Text);

        surface.SelectedObjectName = shapeName;
        Assert.False(panel.IsTextObject);
        Assert.True(panel.IsShapeObject);
        Assert.Equal("", panel.Text); // у фигуры текста нет — поле не должно хранить старое значение
    }

    [Fact]
    public void AvailableDataSources_ReflectsRegisteredSources()
    {
        var (service, surface, panel) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя", "Город" }, Array.Empty<IReadOnlyList<string>>());
        surface.CommitChange();

        Assert.Contains("Клиенты", panel.AvailableDataSources);
        Assert.True(panel.HasDataSources);
    }

    [Fact]
    public void SelectingFieldSource_PopulatesAvailableColumns()
    {
        var (service, surface, panel) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя", "Город" }, Array.Empty<IReadOnlyList<string>>());
        surface.CommitChange();

        panel.SelectedFieldSource = "Клиенты";

        Assert.Equal(new[] { "Имя", "Город" }, panel.AvailableFieldColumns);
    }

    [Fact]
    public void InsertField_AppendsBracketExpressionToText()
    {
        var (service, surface, panel) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя" }, Array.Empty<IReadOnlyList<string>>());
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);
        service.SetText(name, "Имя: ");
        surface.CommitChange();
        surface.SelectedObjectName = name;

        panel.SelectedFieldSource = "Клиенты";
        panel.SelectedFieldColumn = "Имя";
        panel.InsertFieldCommand.Execute(null);

        Assert.Equal("Имя: [Клиенты.Имя]", panel.Text);
        Assert.Equal("Имя: [Клиенты.Имя]", service.GetSnapshot().Pages[0].Bands
            .SelectMany(b => b.Objects).Single(o => o.Name == name).Text);
    }

    [Fact]
    public void InsertField_NoOpWhenNothingSelected()
    {
        var (service, surface, panel) = Create();
        service.SetDataSource("Клиенты", new[] { "Имя" }, Array.Empty<IReadOnlyList<string>>());
        surface.CommitChange();
        service.MarkSaved("dummy.frx");

        panel.InsertFieldCommand.Execute(null); // ни поле, ни колонка не выбраны

        Assert.False(service.IsDirty);
    }
}
