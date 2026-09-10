using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class FastReportServiceTests
{
    private static string DataBandName(FastReportService service) =>
        service.GetSnapshot().Pages[0].Bands.Single(b => b.Kind == BandKind.Data).Name;

    [Fact]
    public void CreateNew_CreatesA4PageWithStandardBands()
    {
        var service = new FastReportService();
        service.CreateNew();

        var snapshot = service.GetSnapshot();
        var page = Assert.Single(snapshot.Pages);

        Assert.InRange(page.Width, 793f, 794f);    // 210 мм
        Assert.InRange(page.Height, 1122f, 1123f); // 297 мм
        Assert.Equal(5, page.Bands.Count);

        Assert.Equal(BandKind.ReportTitle, page.Bands[0].Kind);
        Assert.Equal(BandKind.PageHeader, page.Bands[1].Kind);
        Assert.Equal(BandKind.Data, page.Bands[2].Kind);
        // Вертикальный порядок FastReport: сводка идёт перед футером страницы.
        Assert.Equal(BandKind.ReportSummary, page.Bands[3].Kind);
        Assert.Equal(BandKind.PageFooter, page.Bands[4].Kind);
    }

    [Fact]
    public void CreateNew_BandTopsAreCumulative()
    {
        var service = new FastReportService();
        service.CreateNew();

        var snapshot = service.GetSnapshot();
        var bands = snapshot.Pages[0].Bands;

        // Каждая следующая полоса начинается с суммы высот предыдущих.
        for (var i = 1; i < bands.Count; i++)
            Assert.Equal(bands[i - 1].Top + bands[i - 1].Height, bands[i].Top, 1);
    }

    [Fact]
    public void GetPageSize_DefaultsToA4Portrait_AfterCreateNew()
    {
        var service = new FastReportService();
        service.CreateNew();

        var (preset, landscape) = service.GetPageSize();

        Assert.Equal(PageSizePreset.A4, preset);
        Assert.False(landscape);
    }

    [Fact]
    public void SetPageSize_A3Landscape_UpdatesSnapshotDimensionsAndIsReflectedByGetPageSize()
    {
        var service = new FastReportService();
        service.CreateNew();

        service.SetPageSize(PageSizePreset.A3, landscape: true);

        var page = service.GetSnapshot().Pages[0];
        Assert.Equal(UnitConverter.MmToPx(420), page.Width, 1);
        Assert.Equal(UnitConverter.MmToPx(297), page.Height, 1);

        var (preset, landscape) = service.GetPageSize();
        Assert.Equal(PageSizePreset.A3, preset);
        Assert.True(landscape);
    }

    [Fact]
    public void SetPageSize_TogglingOrientationTwice_ReturnsToOriginalDimensions()
    {
        // Регрессия: ReportPage.Landscape свопает PaperWidth/PaperHeight только когда значение
        // реально МЕНЯЕТСЯ (см. FastReport.xml) — SetPageSize должен приводить страницу к
        // книжной ориентации перед выставлением абсолютных размеров пресета, иначе повторные
        // переключения ориентации туда-обратно могли бы разъехаться.
        var service = new FastReportService();
        service.CreateNew();

        service.SetPageSize(PageSizePreset.A4, landscape: true);
        service.SetPageSize(PageSizePreset.A4, landscape: false);

        var page = service.GetSnapshot().Pages[0];
        Assert.Equal(UnitConverter.MmToPx(210), page.Width, 1);
        Assert.Equal(UnitConverter.MmToPx(297), page.Height, 1);
    }

    [Fact]
    public void AddObject_CreatesTextWithExpectedBounds()
    {
        var service = new FastReportService();
        service.CreateNew();

        var name = service.AddObject(DesignObjectType.Text, 1f, 0.5f, 6f, 1.5f);

        var obj = service.GetSnapshot()
            .Pages[0].Bands.SelectMany(b => b.Objects)
            .Single(o => o.Name == name);

        Assert.Equal(DesignObjectType.Text, obj.Type);
        Assert.Equal(UnitConverter.CmToPx(1f), obj.Bounds.Left, 1);
        Assert.Equal(UnitConverter.CmToPx(0.5f), obj.Bounds.Top, 1);
        Assert.Equal(UnitConverter.CmToPx(6f), obj.Bounds.Width, 1);
        Assert.Equal(UnitConverter.CmToPx(1.5f), obj.Bounds.Height, 1);
    }

    [Fact]
    public void AddObject_CanTargetSpecificBand()
    {
        var service = new FastReportService();
        service.CreateNew();

        var titleBand = service.GetSnapshot().Pages[0].Bands[0];
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2, titleBand.Name);

        var obj = service.GetSnapshot()
            .Pages[0].Bands.Single(b => b.Name == titleBand.Name).Objects
            .Single(o => o.Name == name);

        Assert.Equal(DesignObjectType.Shape, obj.Type);
    }

    [Fact]
    public void MoveAndResize_UpdateBounds()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Line, 0, 0, 3, 1);

        service.MoveObject(name, 2f, 3f);
        service.ResizeObject(name, 5f, 2f);

        var obj = service.GetSnapshot()
            .Pages[0].Bands.SelectMany(b => b.Objects)
            .Single(o => o.Name == name);

        Assert.Equal(UnitConverter.CmToPx(2f), obj.Bounds.Left, 1);
        Assert.Equal(UnitConverter.CmToPx(3f), obj.Bounds.Top, 1);
        Assert.Equal(UnitConverter.CmToPx(5f), obj.Bounds.Width, 1);
        Assert.Equal(UnitConverter.CmToPx(2f), obj.Bounds.Height, 1);
    }

    [Fact]
    public void DeleteObject_RemovesFromSnapshot()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.DeleteObject(name);

        Assert.DoesNotContain(
            service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects),
            o => o.Name == name);
    }

    [Fact]
    public void DeleteObject_ThrowsWhenMissing()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.Throws<KeyNotFoundException>(() => service.DeleteObject("NoSuchObject"));
    }

    [Fact]
    public void AddObject_GeneratesUniqueNames()
    {
        var service = new FastReportService();
        service.CreateNew();

        var a = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);
        var b = service.AddObject(DesignObjectType.Text, 0, 0, 2, 1);

        Assert.NotEqual(a, b);
        Assert.Equal("Text", a);
        Assert.Equal("Text1", b);
    }

    [Fact]
    public void AddBand_AddsWithUniqueNameAndHeight()
    {
        var service = new FastReportService();
        service.CreateNew();
        var before = service.GetSnapshot().Pages[0].Bands.Count;

        var name = service.AddBand(BandKind.Data, 3f);

        var snapshot = service.GetSnapshot().Pages[0];
        Assert.Equal(before + 1, snapshot.Bands.Count);
        var band = snapshot.Bands.Single(b => b.Name == name);
        Assert.Equal(UnitConverter.CmToPx(3f), band.Height, 1);
    }

    [Fact]
    public void AddBand_ThrowsForGroupFooterKind()
    {
        var service = new FastReportService();
        service.CreateNew();

        // GroupFooterBand — вложенная полоса (GroupHeaderBand.GroupFooter), не самостоятельный
        // член page.Bands; без явного выбора родительской полосы FastReport бросает
        // ParentException при попытке добавить её напрямую.
        Assert.Throws<NotSupportedException>(() => service.AddBand(BandKind.GroupFooter));
    }

    [Fact]
    public void AddBand_ThrowsForChildKind()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.Throws<NotSupportedException>(() => service.AddBand(BandKind.Child));
    }

    [Fact]
    public void RemoveBand_RemovesFromSnapshot()
    {
        var service = new FastReportService();
        service.CreateNew();
        var title = service.GetSnapshot().Pages[0].Bands[0].Name;

        service.RemoveBand(title);

        Assert.DoesNotContain(service.GetSnapshot().Pages[0].Bands, b => b.Name == title);
    }

    [Fact]
    public void RenameBand_RenamesBand()
    {
        var service = new FastReportService();
        service.CreateNew();
        var title = service.GetSnapshot().Pages[0].Bands[0].Name;

        service.RenameBand(title, "Заголовок1");

        Assert.DoesNotContain(service.GetSnapshot().Pages[0].Bands, b => b.Name == title);
        Assert.Contains(service.GetSnapshot().Pages[0].Bands, b => b.Name == "Заголовок1");
    }

    [Fact]
    public void RenameBand_ThrowsWhenNameTaken()
    {
        var service = new FastReportService();
        service.CreateNew();
        var bands = service.GetSnapshot().Pages[0].Bands;

        Assert.Throws<InvalidOperationException>(() => service.RenameBand(bands[0].Name, bands[1].Name));
    }

    [Fact]
    public void RenameBand_ThrowsWhenMissing()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.Throws<KeyNotFoundException>(() => service.RenameBand("NoSuchBand", "X"));
    }

    [Fact]
    public void SetBandHeight_UpdatesHeight()
    {
        var service = new FastReportService();
        service.CreateNew();
        var band = service.GetSnapshot().Pages[0].Bands[0];

        service.SetBandHeight(band.Name, 5f);

        var updated = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == band.Name);
        Assert.Equal(UnitConverter.CmToPx(5f), updated.Height, 1);
    }

    [Fact]
    public void SetDataSource_RegistersAndListsColumns()
    {
        var service = new FastReportService();
        service.CreateNew();

        service.SetDataSource("Клиенты", new[] { "Имя", "Город" }, new[]
        {
            new[] { "Иван", "Москва" },
            new[] { "Ольга", "Казань" },
        });

        Assert.Contains("Клиенты", service.GetDataSourceNames());
        Assert.Equal(new[] { "Имя", "Город" }, service.GetDataSourceColumns("Клиенты"));
    }

    [Fact]
    public void SetDataSource_CalledTwice_ReplacesContent()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });

        service.SetDataSource("Клиенты", new[] { "Имя", "Город" }, new[] { new[] { "Ольга", "Казань" } });

        Assert.Equal(new[] { "Имя", "Город" }, service.GetDataSourceColumns("Клиенты"));
        Assert.Single(service.GetDataSourceNames(), n => n == "Клиенты");
    }

    [Fact]
    public void RemoveDataSource_ThrowsWhenMissing()
    {
        var service = new FastReportService();
        service.CreateNew();

        Assert.Throws<KeyNotFoundException>(() => service.RemoveDataSource("NoSuchSource"));
    }

    [Fact]
    public void AssignBandDataSource_BindsDataSourceToBand()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);

        service.AssignBandDataSource(dataBand, "Клиенты");

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Equal("Клиенты", band.DataSourceName);
    }

    [Fact]
    public void AssignBandDataSource_NullDetachesSource()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");

        service.AssignBandDataSource(dataBand, null);

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Null(band.DataSourceName);
    }

    [Fact]
    public void AssignBandDataSource_ThrowsForNonDataBand()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var titleBand = service.GetSnapshot().Pages[0].Bands[0].Name; // ReportTitle

        Assert.Throws<InvalidOperationException>(() => service.AssignBandDataSource(titleBand, "Клиенты"));
    }

    [Fact]
    public void EditingDataSource_KeepsExistingBandAssignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");

        // Правка строк того же источника не должна отвязать его от полосы.
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" }, new[] { "Ольга" } });

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Equal("Клиенты", band.DataSourceName);
    }

    [Fact]
    public void RenameDataSource_KeepsExistingBandAssignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");

        service.RenameDataSource("Клиенты", "Заказчики");

        var band = service.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
        Assert.Equal("Заказчики", band.DataSourceName);
    }

    [Fact]
    public void CreateNew_ClearsPreviouslyRegisteredDataSources()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });

        service.CreateNew();

        Assert.Empty(service.GetDataSourceNames());
    }

    [Fact]
    public void FieldExpression_EvaluatesWithBoundDataOnPrepare()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[]
        {
            new[] { "Иван" },
            new[] { "Ольга" },
        });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");

        var textName = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1, dataBand);
        service.SetText(textName, "[Клиенты.Имя]");

        service.CurrentReport.Prepare();
        var texts = service.CurrentReport.PreparedPages.GetPage(0).AllObjects
            .OfType<FastReport.TextObject>()
            .Where(t => t.Name == textName)
            .Select(t => t.Text)
            .ToList();

        Assert.Equal(new[] { "Иван", "Ольга" }, texts);
    }

    /// <summary>
    /// Регрессия: привязка DataBand.DataSource и компонент TableDataSource сериализуются как
    /// часть Report независимо от sidecar-файла с данными источников (см.
    /// SaveDataSourcesSidecar/LoadDataSourcesSidecar в FastReportService.cs) — если sidecar
    /// недоступен (файл потерян/скопирован без него — см. известные ограничения в
    /// ARCHITECTURE.md), DataBand после Load() ссылался бы на "отключённый от данных" источник,
    /// и Report.Prepare() падал бы с DataTableException прямо из DataBand.InitDataSource(),
    /// даже без единого [Field]-выражения. Воспроизведено вручную пользователем при обычном
    /// открытии сохранённого файла (не придуманный edge case) до появления sidecar-персистентности.
    /// </summary>
    [Fact]
    public void Load_MissingSidecar_DetachesStaleDataSourceFromBand_PrepareDoesNotThrow()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Источник", new[] { "Столбец1" }, new[] { new[] { "значение1" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Источник");

        var path = Path.Combine(Path.GetTempPath(), $"stale_ds_{Guid.NewGuid():N}.frx");
        var sidecarPath = path + ".datasources.json";
        try
        {
            service.Save(path);
            File.Delete(sidecarPath); // симулируем .frx, скопированный/сохранённый без sidecar

            var loaded = new FastReportService();
            loaded.Load(path);

            Assert.Empty(loaded.GetDataSourceNames());
            var band = loaded.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
            Assert.Null(band.DataSourceName); // источник корректно отвязан

            var ex = Record.Exception(() => loaded.CurrentReport.Prepare());
            Assert.Null(ex);
        }
        finally
        {
            File.Delete(path);
            File.Delete(sidecarPath);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsContent()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 1, 1, 4, 1);
        service.MoveObject(name, 2.5f, 1.75f);

        var path = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            var loaded = new FastReportService();
            loaded.Load(path);

            var obj = loaded.GetSnapshot()
                .Pages[0].Bands.SelectMany(b => b.Objects)
                .Single(o => o.Name == name);

            Assert.Equal(UnitConverter.CmToPx(2.5f), obj.Bounds.Left, 1);
            Assert.Equal(UnitConverter.CmToPx(1.75f), obj.Bounds.Top, 0);
            Assert.Equal(5, loaded.GetSnapshot().Pages[0].Bands.Count);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsDataSourceRowsAndBandAssignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[]
        {
            new[] { "Иван" },
            new[] { "Ольга" },
        });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");
        var textName = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1, dataBand);
        service.SetText(textName, "[Клиенты.Имя]");

        var path = Path.Combine(Path.GetTempPath(), $"ds_roundtrip_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            var loaded = new FastReportService();
            loaded.Load(path);

            Assert.Equal(new[] { "Клиенты" }, loaded.GetDataSourceNames());
            Assert.Equal(new[] { "Имя" }, loaded.GetDataSourceColumns("Клиенты"));

            var band = loaded.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
            Assert.Equal("Клиенты", band.DataSourceName);

            // Названия колонок можно было бы получить и без sidecar (они есть в старом тесте
            // FieldExpression_...) — здесь важно проверить, что дошли именно СТРОКИ, которые
            // никакой другой геттер IFastReportService не возвращает.
            loaded.CurrentReport.Prepare();
            var texts = loaded.CurrentReport.PreparedPages.GetPage(0).AllObjects
                .OfType<FastReport.TextObject>()
                .Where(t => t.Name == textName)
                .Select(t => t.Text)
                .ToList();
            Assert.Equal(new[] { "Иван", "Ольга" }, texts);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }

    [Fact]
    public void Load_CorruptSidecar_ReturnsEmptyInsteadOfThrowing()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });

        var path = Path.Combine(Path.GetTempPath(), $"ds_corrupt_{Guid.NewGuid():N}.frx");
        var sidecarPath = path + ".datasources.json";
        try
        {
            service.Save(path);
            File.WriteAllText(sidecarPath, "не json");

            var loaded = new FastReportService();
            var ex = Record.Exception(() => loaded.Load(path));

            Assert.Null(ex);
            Assert.Empty(loaded.GetDataSourceNames());
        }
        finally
        {
            File.Delete(path);
            File.Delete(sidecarPath);
        }
    }

    [Fact]
    public void RenameDataSource_ThenSaveAndLoad_PersistsUnderNewName()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");
        service.RenameDataSource("Клиенты", "Заказчики");

        var path = Path.Combine(Path.GetTempPath(), $"ds_rename_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            var loaded = new FastReportService();
            loaded.Load(path);

            Assert.Equal(new[] { "Заказчики" }, loaded.GetDataSourceNames());
            var band = loaded.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
            Assert.Equal("Заказчики", band.DataSourceName);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }

    [Fact]
    public void LoadAsTemplate_RestoresDataSourcesButNotFilePath()
    {
        var service = new FastReportService();
        service.CreateNew();
        service.SetDataSource("Клиенты", new[] { "Имя" }, new[] { new[] { "Иван" } });
        var dataBand = DataBandName(service);
        service.AssignBandDataSource(dataBand, "Клиенты");

        var path = Path.Combine(Path.GetTempPath(), $"ds_template_{Guid.NewGuid():N}.frx");
        try
        {
            service.Save(path);

            var loaded = new FastReportService();
            loaded.LoadAsTemplate(path);

            Assert.Null(loaded.CurrentFilePath); // документ остаётся «Новым»
            Assert.Equal(new[] { "Клиенты" }, loaded.GetDataSourceNames());
            var band = loaded.GetSnapshot().Pages[0].Bands.Single(b => b.Name == dataBand);
            Assert.Equal("Клиенты", band.DataSourceName);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".datasources.json");
        }
    }
}