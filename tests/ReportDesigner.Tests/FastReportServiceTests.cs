using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class FastReportServiceTests
{
    [Fact]
    public void CreateNew_CreatesA4PageWithStandardBands()
    {
        var service = new FastReportService();
        service.CreateNew();

        var snapshot = service.GetSnapshot();
        var page = Assert.Single(snapshot.Pages);

        Assert.InRange(page.Width, 793f, 794f);    // 210 мм
        Assert.InRange(page.Height, 1118f, 1119f); // 297 мм
        Assert.Equal(5, page.Bands.Count);

        Assert.Equal(BandKind.ReportTitle, page.Bands[0].Kind);
        Assert.Equal(BandKind.PageHeader, page.Bands[1].Kind);
        Assert.Equal(BandKind.Data, page.Bands[2].Kind);
        Assert.Equal(BandKind.PageFooter, page.Bands[3].Kind);
        Assert.Equal(BandKind.ReportSummary, page.Bands[4].Kind);
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
    public void RemoveBand_RemovesFromSnapshot()
    {
        var service = new FastReportService();
        service.CreateNew();
        var title = service.GetSnapshot().Pages[0].Bands[0].Name;

        service.RemoveBand(title);

        Assert.DoesNotContain(service.GetSnapshot().Pages[0].Bands, b => b.Name == title);
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
            Assert.Equal(UnitConverter.CmToPx(1.75f), obj.Bounds.Top, 1);
            Assert.Equal(5, loaded.GetSnapshot().Pages[0].Bands.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}