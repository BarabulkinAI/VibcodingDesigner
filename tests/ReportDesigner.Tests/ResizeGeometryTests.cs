using System.Drawing;
using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class ResizeGeometryTests
{
    private static readonly RectangleF Bounds = new(10f, 20f, 100f, 40f);

    [Fact]
    public void GetHandles_ReturnsEightHandlesCenteredOnCorrectPoints()
    {
        var handles = ResizeGeometry.GetHandles(Bounds, 6f);

        Assert.Equal(8, handles.Count);

        var topLeft = handles.Single(h => h.Handle == ResizeHandle.TopLeft).Rect;
        Assert.Equal(Bounds.Left, topLeft.X + topLeft.Width / 2f, 3);
        Assert.Equal(Bounds.Top, topLeft.Y + topLeft.Height / 2f, 3);

        var bottomRight = handles.Single(h => h.Handle == ResizeHandle.BottomRight).Rect;
        Assert.Equal(Bounds.Right, bottomRight.X + bottomRight.Width / 2f, 3);
        Assert.Equal(Bounds.Bottom, bottomRight.Y + bottomRight.Height / 2f, 3);

        var right = handles.Single(h => h.Handle == ResizeHandle.Right).Rect;
        Assert.Equal(Bounds.Right, right.X + right.Width / 2f, 3);
        Assert.Equal(Bounds.Top + Bounds.Height / 2f, right.Y + right.Height / 2f, 3);
    }

    [Fact]
    public void HitTest_FindsHandleUnderPoint()
    {
        var handle = ResizeGeometry.HitTest(Bounds, 6f, new PointF(Bounds.Right, Bounds.Bottom));
        Assert.Equal(ResizeHandle.BottomRight, handle);
    }

    [Fact]
    public void HitTest_ReturnsNullWhenPointFarFromAnyHandle()
    {
        var handle = ResizeGeometry.HitTest(Bounds, 6f, new PointF(Bounds.Left + Bounds.Width / 2f, Bounds.Top + Bounds.Height / 2f));
        Assert.Null(handle);
    }

    [Fact]
    public void ApplyResize_BottomRight_GrowsWidthAndHeight()
    {
        var result = ResizeGeometry.ApplyResize(Bounds, ResizeHandle.BottomRight, dxPx: 10f, dyPx: 5f, minSizePx: 4f);

        Assert.Equal(Bounds.Left, result.Left);
        Assert.Equal(Bounds.Top, result.Top);
        Assert.Equal(Bounds.Width + 10f, result.Width, 3);
        Assert.Equal(Bounds.Height + 5f, result.Height, 3);
    }

    [Fact]
    public void ApplyResize_TopLeft_MovesOriginAndShrinks()
    {
        var result = ResizeGeometry.ApplyResize(Bounds, ResizeHandle.TopLeft, dxPx: 10f, dyPx: 5f, minSizePx: 4f);

        Assert.Equal(Bounds.Left + 10f, result.Left, 3);
        Assert.Equal(Bounds.Top + 5f, result.Top, 3);
        Assert.Equal(Bounds.Right, result.Right, 3);
        Assert.Equal(Bounds.Bottom, result.Bottom, 3);
    }

    [Fact]
    public void ApplyResize_ClampsToMinimumSize()
    {
        // Тащим правую сторону далеко влево — ширина не должна уйти ниже minSizePx.
        var result = ResizeGeometry.ApplyResize(Bounds, ResizeHandle.Right, dxPx: -1000f, dyPx: 0f, minSizePx: 4f);

        Assert.Equal(4f, result.Width, 3);
        Assert.Equal(Bounds.Left, result.Left, 3);
    }

    [Fact]
    public void ApplyResize_LeftRightHandle_NeverGrowsZeroHeight()
    {
        // Регрессия: у линии (Height = 0) ручки Left/Right не должны трогать высоту вообще —
        // раньше minSizePx применялся к высоте безусловно, даже когда её не двигали, и линия
        // "подпрыгивала" на minSizePx при любом изменении длины.
        var line = new RectangleF(10f, 20f, 50f, 0f);

        var result = ResizeGeometry.ApplyResize(line, ResizeHandle.Right, dxPx: 20f, dyPx: 0f, minSizePx: 8f);

        Assert.Equal(0f, result.Height, 3);
        Assert.Equal(70f, result.Width, 3);
    }

    [Fact]
    public void ApplyResize_TopBottomHandle_NeverGrowsZeroWidth()
    {
        var verticalLine = new RectangleF(10f, 20f, 0f, 50f);

        var result = ResizeGeometry.ApplyResize(verticalLine, ResizeHandle.Bottom, dxPx: 0f, dyPx: 15f, minSizePx: 8f);

        Assert.Equal(0f, result.Width, 3);
        Assert.Equal(65f, result.Height, 3);
    }

    [Fact]
    public void AllowedHandles_ForLine_IsOnlyLeftAndRight()
    {
        var allowed = ResizeGeometry.AllowedHandles(DesignObjectType.Line);

        Assert.NotNull(allowed);
        Assert.Equal(new HashSet<ResizeHandle> { ResizeHandle.Left, ResizeHandle.Right }, allowed);
    }

    [Fact]
    public void AllowedHandles_ForShape_IsNull()
    {
        Assert.Null(ResizeGeometry.AllowedHandles(DesignObjectType.Shape));
    }

    [Fact]
    public void GetHandles_WithAllowedHandlesFilter_ReturnsOnlyThose()
    {
        var handles = ResizeGeometry.GetHandles(Bounds, 6f, ResizeGeometry.AllowedHandles(DesignObjectType.Line));

        Assert.Equal(2, handles.Count);
        Assert.All(handles, h => Assert.True(h.Handle is ResizeHandle.Left or ResizeHandle.Right));
    }

    [Fact]
    public void ClampVertical_LeavesBoundsInsideBandUnchanged()
    {
        var bounds = new RectangleF(0f, 10f, 50f, 20f);
        var result = ResizeGeometry.ClampVertical(bounds, bandHeightPx: 100f);

        Assert.Equal(bounds, result);
    }

    [Fact]
    public void ClampVertical_PushesBoundsBelowZeroDown()
    {
        var bounds = new RectangleF(0f, -10f, 50f, 20f);
        var result = ResizeGeometry.ClampVertical(bounds, bandHeightPx: 100f);

        Assert.Equal(0f, result.Top, 3);
        Assert.Equal(20f, result.Height, 3);
    }

    [Fact]
    public void ClampVertical_PushesBoundsPastBottomUp()
    {
        var bounds = new RectangleF(0f, 90f, 50f, 20f); // bottom = 110, band height = 100
        var result = ResizeGeometry.ClampVertical(bounds, bandHeightPx: 100f);

        Assert.Equal(100f, result.Bottom, 3);
        Assert.Equal(20f, result.Height, 3);
    }

    [Fact]
    public void ClampVertical_CollapsesTallerThanBandToFullBand()
    {
        var bounds = new RectangleF(0f, -20f, 50f, 200f); // выше полосы целиком
        var result = ResizeGeometry.ClampVertical(bounds, bandHeightPx: 100f);

        Assert.Equal(0f, result.Top, 3);
        Assert.Equal(100f, result.Height, 3);
    }
}
