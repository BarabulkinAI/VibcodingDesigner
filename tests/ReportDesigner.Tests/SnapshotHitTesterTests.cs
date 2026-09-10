using System.Drawing;
using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class SnapshotHitTesterTests
{
    private static DesignSnapshot BuildSnapshot(params BandSnapshot[] bands) => BuildSnapshot(marginLeft: 0f, bands);

    private static DesignSnapshot BuildSnapshot(float marginLeft, params BandSnapshot[] bands) => new()
    {
        Pages = new[]
        {
            new PageSnapshot { Name = "Page1", Width = 800f, Height = 1000f, MarginLeft = marginLeft, Bands = bands }
        }
    };

    private static DesignObjectInfo MakeObject(string name, RectangleF bounds, bool visible = true) => new()
    {
        Name = name,
        Type = DesignObjectType.Shape,
        Bounds = bounds,
        Visible = visible,
    };

    [Fact]
    public void FindObjectAt_ReturnsObjectContainingPoint()
    {
        var obj = MakeObject("Shape1", new RectangleF(10f, 5f, 30f, 20f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 50f, Height = 40f, Objects = new[] { obj } };
        var snapshot = BuildSnapshot(band);

        // band.Top=50, объект в полосе на (10,5)-(40,25) → на странице (10,55)-(40,75)
        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(20f, 60f));

        Assert.NotNull(hit);
        Assert.Equal("Data", hit.Value.BandName);
        Assert.Equal("Shape1", hit.Value.Object.Name);
    }

    [Fact]
    public void FindObjectAt_SubtractsPageMarginLeftFromClickX()
    {
        // Регрессия: объект хранит X относительно левого края полосы, а реальный движок FastReport
        // рисует полосы со сдвигом на MarginLeft от края бумаги — клик в координатах страницы
        // должен сначала вычесть этот сдвиг, иначе объект с Left=0 не находился бы кликом по
        // видимому на превью месту.
        var obj = MakeObject("Shape1", new RectangleF(0f, 0f, 30f, 20f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 50f, Height = 40f, Objects = new[] { obj } };
        var snapshot = BuildSnapshot(marginLeft: 40f, band);

        // Объект на странице фактически занимает (40,50)-(70,70) — клик по (50,60) должен попасть.
        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(50f, 60f));

        Assert.NotNull(hit);
        Assert.Equal("Shape1", hit.Value.Object.Name);
    }

    [Fact]
    public void FindObjectAt_ReturnsNullWhenPointInBandButOutsideObject()
    {
        var obj = MakeObject("Shape1", new RectangleF(10f, 5f, 30f, 20f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 50f, Height = 40f, Objects = new[] { obj } };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(500f, 60f));

        Assert.Null(hit);
    }

    [Fact]
    public void FindObjectAt_ReturnsNullWhenPointOutsideAllBands()
    {
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 50f, Height = 40f, Objects = Array.Empty<DesignObjectInfo>() };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(20f, 500f));

        Assert.Null(hit);
    }

    [Fact]
    public void FindObjectAt_PrefersTopmostObjectOnOverlap()
    {
        var bottom = MakeObject("Bottom", new RectangleF(0f, 0f, 50f, 50f));
        var top = MakeObject("Top", new RectangleF(0f, 0f, 50f, 50f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 0f, Height = 100f, Objects = new[] { bottom, top } };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(10f, 10f));

        Assert.NotNull(hit);
        Assert.Equal("Top", hit.Value.Object.Name);
    }

    [Fact]
    public void FindObjectAt_SkipsInvisibleObjects()
    {
        var invisible = MakeObject("Hidden", new RectangleF(0f, 0f, 50f, 50f), visible: false);
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 0f, Height = 100f, Objects = new[] { invisible } };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(10f, 10f));

        Assert.Null(hit);
    }

    [Fact]
    public void FindObjectAt_ReturnsNullForEmptySnapshot()
    {
        var snapshot = new DesignSnapshot();

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(10f, 10f));

        Assert.Null(hit);
    }

    [Fact]
    public void FindObjectAt_HitsZeroHeightLine_NearItsCenterline()
    {
        // Регрессия: RectangleF.Contains у прямоугольника нулевой высоты (горизонтальная линия)
        // не матчит вообще никакую точку — без допуска такую линию невозможно выделить кликом.
        var line = MakeObject("Line1", new RectangleF(10f, 20f, 60f, 0f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 0f, Height = 100f, Objects = new[] { line } };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(30f, 21f)); // 1px мимо линии по вертикали

        Assert.NotNull(hit);
        Assert.Equal("Line1", hit.Value.Object.Name);
    }

    [Fact]
    public void FindObjectAt_MissesZeroHeightLine_FarFromCenterline()
    {
        var line = MakeObject("Line1", new RectangleF(10f, 20f, 60f, 0f));
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 0f, Height = 100f, Objects = new[] { line } };
        var snapshot = BuildSnapshot(band);

        var hit = SnapshotHitTester.FindObjectAt(snapshot, new PointF(30f, 40f)); // далеко по вертикали от линии

        Assert.Null(hit);
    }

    [Fact]
    public void FindBandAt_ReturnsBandContainingPoint()
    {
        var band1 = new BandSnapshot { Name = "Title", Kind = BandKind.ReportTitle, Top = 0f, Height = 50f, Objects = Array.Empty<DesignObjectInfo>() };
        var band2 = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 50f, Height = 40f, Objects = Array.Empty<DesignObjectInfo>() };
        var snapshot = BuildSnapshot(band1, band2);

        var band = SnapshotHitTester.FindBandAt(snapshot, new PointF(20f, 60f));

        Assert.NotNull(band);
        Assert.Equal("Data", band!.Name);
    }

    [Fact]
    public void FindBandAt_ReturnsNullWhenPointBelowAllBands()
    {
        var band = new BandSnapshot { Name = "Data", Kind = BandKind.Data, Top = 0f, Height = 40f, Objects = Array.Empty<DesignObjectInfo>() };
        var snapshot = BuildSnapshot(band);

        var found = SnapshotHitTester.FindBandAt(snapshot, new PointF(20f, 500f));

        Assert.Null(found);
    }

    [Fact]
    public void FindBandAt_ReturnsNullForEmptySnapshot()
    {
        var snapshot = new DesignSnapshot();

        var found = SnapshotHitTester.FindBandAt(snapshot, new PointF(10f, 10f));

        Assert.Null(found);
    }
}
