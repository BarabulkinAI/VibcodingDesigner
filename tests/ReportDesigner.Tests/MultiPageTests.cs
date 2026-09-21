using System.Drawing;
using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

/// <summary>Несколько страниц в отчёте: активная страница в сервисе, добавление/удаление,
/// сохранение/загрузка, Undo/Redo, снимок/hit-test, вкладки в DesignSurfaceViewModel и превью.</summary>
public class MultiPageTests
{
    private static FastReportService CreateService()
    {
        var service = new FastReportService();
        service.CreateNew();
        return service;
    }

    private static PageSnapshot ActivePage(FastReportService service) => service.GetSnapshot().ActivePage!;

    private static string DataBandName(PageSnapshot page) => page.Bands.Single(b => b.Kind == BandKind.Data).Name;

    // --- Сервис ---

    [Fact]
    public void AddPage_AddsStandardPageMakesItActiveAndMarksDirty()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        service.MarkSaved("fixture.frx");

        var added = service.AddPage();

        var snapshot = service.GetSnapshot();
        Assert.Equal(2, snapshot.Pages.Count);
        Assert.NotEqual(first, added);
        Assert.Equal(added, service.ActivePageName);
        Assert.Equal(added, snapshot.ActivePage!.Name);
        Assert.Equal(5, snapshot.ActivePage.Bands.Count);
        Assert.True(service.IsDirty);
        // Имена полос уникальны на весь отчёт — иначе поиск по имени был бы неоднозначен.
        var allNames = snapshot.Pages.SelectMany(p => p.Bands).Select(b => b.Name).ToList();
        Assert.Equal(allNames.Count, allNames.Distinct().Count());
    }

    [Fact]
    public void SetActivePage_DoesNotDirtyDocument_AndRedirectsPageScopedOperations()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        service.AddPage();
        service.MarkSaved("fixture.frx");

        service.SetActivePage(first);

        Assert.False(service.IsDirty);
        Assert.Equal(first, service.GetSnapshot().ActivePage!.Name);

        service.SetPageSize(PageSizePreset.A3, landscape: true);
        var pages = service.GetSnapshot().Pages;
        Assert.True(pages.Single(p => p.Name == first).Width > pages.Single(p => p.Name == first).Height);
        Assert.True(pages.Single(p => p.Name != first).Width < pages.Single(p => p.Name != first).Height); // вторая осталась книжной A4
        Assert.Equal((PageSizePreset.A3, true), service.GetPageSize());
    }

    [Fact]
    public void AddBand_GoesToActivePage()
    {
        var service = CreateService();
        service.AddPage();
        var secondCountBefore = ActivePage(service).Bands.Count;

        service.AddBand(BandKind.GroupHeader, 1f);

        var snapshot = service.GetSnapshot();
        Assert.Equal(secondCountBefore + 1, snapshot.ActivePage!.Bands.Count);
        Assert.Equal(5, snapshot.Pages.First(p => p.Name != snapshot.ActivePageName).Bands.Count);
    }

    [Fact]
    public void SetActivePage_UnknownName_Throws()
    {
        var service = CreateService();

        Assert.Throws<KeyNotFoundException>(() => service.SetActivePage("Нет такой"));
    }

    [Fact]
    public void RemovePage_LastPage_Throws()
    {
        var service = CreateService();

        Assert.Throws<InvalidOperationException>(() => service.RemovePage(service.ActivePageName));
    }

    [Fact]
    public void RemovePage_ActivePage_MakesNeighbourActive()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        var second = service.AddPage();

        service.RemovePage(second);

        Assert.Equal(first, service.ActivePageName);
        Assert.Single(service.GetSnapshot().Pages);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void RemovePage_InactivePage_KeepsActiveOne()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        var second = service.AddPage();

        service.RemovePage(first);

        Assert.Equal(second, service.ActivePageName);
    }

    [Fact]
    public void ObjectOperationsByName_WorkAcrossPages()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        service.AddPage();
        var name = service.AddObject(DesignObjectType.Text, 1, 1, 2, 1); // на второй странице
        service.SetActivePage(first);

        service.MoveObject(name, 3, 3); // объект другой страницы, активна первая
        service.DeleteObject(name);

        Assert.All(service.GetSnapshot().Pages, p => Assert.Empty(p.Bands.SelectMany(b => b.Objects)));
    }

    [Fact]
    public void RemoveBand_ByNameFromInactivePage()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        var secondBands = ActivePageAfterAdd(service);
        service.SetActivePage(first);

        service.RemoveBand(secondBands.Bands[0].Name);

        Assert.Equal(4, service.GetSnapshot().Pages.Single(p => p.Name == secondBands.Name).Bands.Count);

        static PageSnapshot ActivePageAfterAdd(FastReportService s)
        {
            s.AddPage();
            return s.GetSnapshot().ActivePage!;
        }
    }

    // --- Сохранение/загрузка ---

    [Fact]
    public void SaveAndLoad_RoundTripsPagesAndDataBandAssignmentOnSecondPage()
    {
        var service = CreateService();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        service.AddPage();
        service.SetPageSize(PageSizePreset.A3, landscape: true);
        var secondPage = ActivePage(service);
        var secondDataBand = DataBandName(secondPage);
        service.AssignBandDataSource(secondDataBand, "Клиенты");

        var path = Path.Combine(Path.GetTempPath(), $"multipage_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);
            var loaded = new FastReportService();
            loaded.Load(path);

            var snapshot = loaded.GetSnapshot();
            Assert.Equal(2, snapshot.Pages.Count);
            Assert.Equal(snapshot.Pages[0].Name, loaded.ActivePageName); // после Load активна первая
            var loadedSecond = snapshot.Pages[1];
            Assert.True(loadedSecond.Width > loadedSecond.Height);
            Assert.Equal("Клиенты", loadedSecond.Bands.Single(b => b.Name == secondDataBand).DataSourceName);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }

    [Fact]
    public void EditingDataSourceRows_KeepsBandAssignmentOnSecondPage()
    {
        var service = CreateService();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        service.AddPage();
        var secondDataBand = DataBandName(ActivePage(service));
        service.AssignBandDataSource(secondDataBand, "Клиенты");

        // Пересборка источников читает привязки с живых полос — раньше смотрела только первую страницу.
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" }, new[] { "Ольга" } });

        Assert.Equal("Клиенты", ActivePage(service).Bands.Single(b => b.Name == secondDataBand).DataSourceName);
    }

    // --- Undo/Redo ---

    [Fact]
    public void Undo_OfAddPage_FallsBackToExistingPage()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        service.Checkpoint();
        service.AddPage();
        service.Checkpoint();

        service.Undo();

        Assert.Equal(first, service.ActivePageName);
        Assert.Single(service.GetSnapshot().Pages);

        service.Redo();

        Assert.Equal(2, service.GetSnapshot().Pages.Count);
    }

    [Fact]
    public void Undo_KeepsActivePage_WhenItStillExists()
    {
        var service = CreateService();
        var first = service.ActivePageName;
        var second = service.AddPage();
        service.Checkpoint();
        service.AddObject(DesignObjectType.Text, 1, 1, 2, 1);
        service.Checkpoint();

        service.Undo();

        Assert.Equal(second, service.ActivePageName);
        Assert.NotEqual(first, service.ActivePageName);
    }

    // --- Снимок и hit-test ---

    [Fact]
    public void Snapshot_ActivePage_FallsBackToFirstWhenNameUnknown()
    {
        var snapshot = new DesignSnapshot
        {
            Pages = new[]
            {
                new PageSnapshot { Name = "A", Width = 1, Height = 1 },
                new PageSnapshot { Name = "B", Width = 1, Height = 1 },
            },
            ActivePageName = "нет",
        };

        Assert.Equal("A", snapshot.ActivePage!.Name);
        Assert.Null(new DesignSnapshot().ActivePage);
    }

    [Fact]
    public void HitTester_UsesActivePage()
    {
        var onFirst = new DesignObjectInfo { Name = "Shape1", Type = DesignObjectType.Shape, Bounds = new RectangleF(0, 0, 30, 20), Visible = true };
        var onSecond = new DesignObjectInfo { Name = "Shape2", Type = DesignObjectType.Shape, Bounds = new RectangleF(0, 0, 30, 20), Visible = true };
        var snapshot = new DesignSnapshot
        {
            Pages = new[]
            {
                new PageSnapshot
                {
                    Name = "P1", Width = 800, Height = 1000,
                    Bands = new[] { new BandSnapshot { Name = "B1", Kind = BandKind.Data, Top = 0, Height = 50, Objects = new[] { onFirst } } },
                },
                new PageSnapshot
                {
                    Name = "P2", Width = 800, Height = 1000, MarginLeft = 40,
                    Bands = new[] { new BandSnapshot { Name = "B2", Kind = BandKind.Data, Top = 0, Height = 50, Objects = new[] { onSecond } } },
                },
            },
            ActivePageName = "P2",
        };

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(50, 10)); // 50 - MarginLeft(40) = 10 внутри объекта

        Assert.Equal("Shape2", hit!.Value.Object.Name);
        Assert.Null(SnapshotHitTester.FindObjectAt(snapshot, new PointF(10, 10))); // левее поля второй страницы
    }

    // --- DesignSurfaceViewModel / дерево ---

    [Fact]
    public void PageTabs_ReflectPagesAndActiveOne()
    {
        var service = CreateService();
        var vm = new DesignSurfaceViewModel(service);

        Assert.Equal(new[] { "Стр. 1" }, vm.PageTabs.Select(t => t.Title));
        Assert.False(vm.RemovePageCommand.CanExecute(null));

        vm.AddPageCommand.Execute(null);

        Assert.Equal(new[] { "Стр. 1", "Стр. 2" }, vm.PageTabs.Select(t => t.Title));
        Assert.Equal(new[] { false, true }, vm.PageTabs.Select(t => t.IsActive));
        Assert.True(vm.RemovePageCommand.CanExecute(null));
        Assert.True(vm.CanUndo); // добавление страницы — мутация документа с чекпойнтом
    }

    [Fact]
    public void SelectPage_SwitchesSnapshot_ClearsSelection_AndDoesNotRaiseDocumentChanged()
    {
        var service = CreateService();
        var vm = new DesignSurfaceViewModel(service);
        var first = vm.PageTabs[0].Name;
        vm.AddPageCommand.Execute(null);
        vm.SelectedObjectName = "чтоугодно";
        vm.SelectedBandName = "чтоугодно";
        service.MarkSaved("fixture.frx");
        var documentChanged = 0;
        var pageChanged = 0;
        vm.DocumentChanged += () => documentChanged++;
        vm.ActivePageChanged += () => pageChanged++;

        vm.SelectPageCommand.Execute(first);

        Assert.Equal(first, vm.Snapshot.ActivePage!.Name);
        Assert.Null(vm.SelectedObjectName);
        Assert.Null(vm.SelectedBandName);
        Assert.Equal(0, documentChanged);
        Assert.Equal(1, pageChanged);
        Assert.False(service.IsDirty);
        Assert.Equal(new[] { true, false }, vm.PageTabs.Select(t => t.IsActive));
    }

    [Fact]
    public void RemovePage_RemovesActivePageAndCanBeUndone()
    {
        var service = CreateService();
        var vm = new DesignSurfaceViewModel(service);
        vm.AddPageCommand.Execute(null);

        vm.RemovePageCommand.Execute(null);

        Assert.Single(vm.Snapshot.Pages);

        vm.UndoCommand.Execute(null);

        Assert.Equal(2, vm.Snapshot.Pages.Count);
    }

    [Fact]
    public void PlaceObjectAt_PutsObjectOnActivePage()
    {
        var service = CreateService();
        var vm = new DesignSurfaceViewModel(service);
        var first = vm.PageTabs[0].Name;
        vm.AddPageCommand.Execute(null);
        var secondPage = vm.Snapshot.ActivePage!;
        var band = secondPage.Bands.First();

        vm.SelectToolCommand.Execute(DesignObjectType.Text);
        vm.PlaceObjectAt(new PointF(secondPage.MarginLeft + 20, band.Top + 5));

        Assert.Single(vm.Snapshot.Pages.Single(p => p.Name == secondPage.Name).Bands.SelectMany(b => b.Objects));
        Assert.Empty(vm.Snapshot.Pages.Single(p => p.Name == first).Bands.SelectMany(b => b.Objects));
    }

    [Fact]
    public void ObjectTree_ShowsBandsOfActivePage_AndFollowsSwitching()
    {
        var service = CreateService();
        var surface = new DesignSurfaceViewModel(service);
        var tree = new ObjectTreeViewModel(service, surface);
        var first = surface.PageTabs[0].Name;
        surface.AddPageCommand.Execute(null);
        var secondBandNames = surface.Snapshot.ActivePage!.Bands.Select(b => b.Name).ToList();

        Assert.Equal(secondBandNames, tree.Bands.Select(b => b.Name));

        surface.SelectPageCommand.Execute(first);

        Assert.DoesNotContain(tree.Bands, b => secondBandNames.Contains(b.Name));
        Assert.Equal(5, tree.Bands.Count);
    }

    // --- Превью/экспорт (эмпирическая проверка: движок отдаёт все страницы) ---

    private static (int Width, int Height) PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        static int ReadInt(byte[] b, int offset) => (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
        return (ReadInt(bytes, 16), ReadInt(bytes, 20)); // IHDR: ширина, высота
    }

    [Fact]
    public void ExportPng_ContainsAllPages()
    {
        var single = CreateService();
        var two = CreateService();
        two.AddPage();
        var singlePath = Path.Combine(Path.GetTempPath(), $"mp_single_{Guid.NewGuid():N}.png");
        var twoPath = Path.Combine(Path.GetTempPath(), $"mp_two_{Guid.NewGuid():N}.png");
        try
        {
            var export = new ExportService();
            export.ExportPng(single.CurrentReport, singlePath);
            export.ExportPng(two.CurrentReport, twoPath);

            var (singleWidth, singleHeight) = PngSize(singlePath);
            var (twoWidth, twoHeight) = PngSize(twoPath);
            Assert.Equal(singleWidth, twoWidth);
            Assert.InRange(twoHeight, singleHeight * 2 - 5, singleHeight * 2 + 5);
        }
        finally
        {
            File.Delete(singlePath);
            File.Delete(twoPath);
        }
    }

    [Fact]
    public void ExportPng_ObjectAtOrigin_IsRenderedAtSheetCorner()
    {
        // Эмпирическая проверка нулевых полей новых страниц: чёрный прямоугольник в (0,0) верхней
        // полосы должен закрашивать самый угол листа, а не начинаться с отступа 10 мм (~38 px).
        var service = CreateService();
        var titleBand = service.GetSnapshot().ActivePage!.Bands.First(b => b.Kind == BandKind.ReportTitle).Name;
        var shape = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 1, titleBand);
        service.SetFillColor(shape, Color.Black);
        var path = Path.Combine(Path.GetTempPath(), $"mp_corner_{Guid.NewGuid():N}.png");
        try
        {
            new ExportService().ExportPng(service.CurrentReport, path);

            using var bitmap = SkiaSharp.SKBitmap.Decode(path);
            Assert.Equal(0xFF000000u, (uint)bitmap.GetPixel(3, 3));
            Assert.NotEqual(0xFF000000u, (uint)bitmap.GetPixel(bitmap.Width - 3, 3));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
