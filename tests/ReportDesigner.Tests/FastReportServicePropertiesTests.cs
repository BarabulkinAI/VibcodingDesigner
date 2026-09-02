using System.Drawing;
using System.Linq;
using ReportDesigner.Models;
using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class FastReportServicePropertiesTests
{
    private static DesignObjectInfo ObjectByName(FastReportService service, string name) =>
        service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects).Single(o => o.Name == name);

    private static IReadOnlyList<string> DataBandObjectNames(FastReportService service) =>
        service.GetSnapshot().Pages[0].Bands
            .Single(b => b.Kind == BandKind.Data).Objects
            .Select(o => o.Name).ToList();

    [Fact]
    public void SetText_UpdatesTextObjectText()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        service.SetText(name, "Привет");

        Assert.Equal("Привет", ObjectByName(service, name).Text);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void SetText_ThrowsWhenNotTextObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        Assert.Throws<InvalidOperationException>(() => service.SetText(name, "x"));
    }

    [Fact]
    public void SetFont_UpdatesFontFields()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        service.SetFont(name, "Calibri", 14f, bold: true, italic: true);

        var obj = ObjectByName(service, name);
        Assert.Equal("Calibri", obj.FontName);
        Assert.Equal(14f, obj.FontSize, 2);
        Assert.True(obj.FontBold);
        Assert.True(obj.FontItalic);
    }

    [Fact]
    public void SetTextColor_UpdatesTextColor()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        service.SetTextColor(name, Color.Red);

        Assert.Equal(Color.Red.ToArgb(), ObjectByName(service, name).TextColor.ToArgb());
    }

    [Fact]
    public void SetHorizontalAndVerticalAlign_UpdateAlignment()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        service.SetHorizontalAlign(name, DesignTextAlign.Right);
        service.SetVerticalAlign(name, DesignVerticalAlign.Bottom);

        var obj = ObjectByName(service, name);
        Assert.Equal(DesignTextAlign.Right, obj.HorizontalAlign);
        Assert.Equal(DesignVerticalAlign.Bottom, obj.VerticalAlign);
    }

    [Fact]
    public void SetBorder_UpdatesShowWidthAndColor_OnTextObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        service.SetBorder(name, show: true, widthCm: 0.2f, Color.Blue);

        var obj = ObjectByName(service, name);
        Assert.True(obj.ShowBorder);
        Assert.Equal(UnitConverter.CmToPx(0.2f), obj.BorderWidth, 1);
        Assert.Equal(Color.Blue.ToArgb(), obj.BorderColor.ToArgb());
    }

    [Fact]
    public void SetBorder_HideClearsShowBorder()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SetBorder(name, show: false, widthCm: 0.1f, Color.Black);

        Assert.False(ObjectByName(service, name).ShowBorder);
    }

    [Fact]
    public void SetFillColor_UpdatesShapeFillColor()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SetFillColor(name, Color.Green);

        Assert.Equal(Color.Green.ToArgb(), ObjectByName(service, name).FillColor.ToArgb());
    }

    [Fact]
    public void SetFillColor_ThrowsWhenNotShapeObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Text, 0, 0, 4, 1);

        Assert.Throws<InvalidOperationException>(() => service.SetFillColor(name, Color.Green));
    }

    [Fact]
    public void SetShapeKind_UpdatesShapeKind()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SetShapeKind(name, DesignShapeKind.Ellipse);

        Assert.Equal(DesignShapeKind.Ellipse, ObjectByName(service, name).Shape);
    }

    [Fact]
    public void SetLineStyle_UpdatesWidthAndColor()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Line, 0, 0, 3, 0);

        service.SetLineStyle(name, 0.3f, Color.Purple);

        var obj = ObjectByName(service, name);
        Assert.Equal(Color.Purple.ToArgb(), obj.LineColor.ToArgb());
        Assert.Equal(UnitConverter.CmToPx(0.3f), obj.LineWidth, 1);
    }

    [Fact]
    public void SetLineStyle_ThrowsWhenNotLineObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        Assert.Throws<InvalidOperationException>(() => service.SetLineStyle(name, 0.1f, Color.Black));
    }

    [Fact]
    public void SetImage_UpdatesHasImage()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Picture, 0, 0, 3, 3);
        var imagePath = CreateTempPngFile();

        try
        {
            service.SetImage(name, imagePath);

            Assert.True(ObjectByName(service, name).HasImage);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void SetImage_ThrowsWhenNotPictureObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var imagePath = CreateTempPngFile();

        try
        {
            Assert.Throws<InvalidOperationException>(() => service.SetImage(name, imagePath));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ClearImage_ResetsHasImage()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Picture, 0, 0, 3, 3);
        var imagePath = CreateTempPngFile();

        try
        {
            service.SetImage(name, imagePath);
            service.ClearImage(name);

            Assert.False(ObjectByName(service, name).HasImage);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string CreateTempPngFile()
    {
        var path = Path.GetTempFileName() + ".png";
        using var bitmap = new Bitmap(4, 4);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public void SetVisible_UpdatesVisibility()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SetVisible(name, false);

        Assert.False(ObjectByName(service, name).Visible);
    }

    [Fact]
    public void SetName_RenamesObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var name = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SetName(name, "MyShape");

        Assert.Equal("MyShape", ObjectByName(service, "MyShape").Name);
        Assert.DoesNotContain(
            service.GetSnapshot().Pages[0].Bands.SelectMany(b => b.Objects),
            o => o.Name == name);
    }

    [Fact]
    public void SetName_ThrowsOnCollisionWithExistingName()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 3, 0, 2, 2);

        Assert.Throws<InvalidOperationException>(() => service.SetName(b, a));
    }

    [Fact]
    public void BringToFront_MovesObjectToEndOfBandObjects()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var c = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.BringToFront(a);

        Assert.Equal(new[] { b, c, a }, DataBandObjectNames(service));
    }

    [Fact]
    public void SendToBack_MovesObjectToStartOfBandObjects()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var c = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.SendToBack(c);

        Assert.Equal(new[] { c, a, b }, DataBandObjectNames(service));
    }

    [Fact]
    public void MoveForward_SwapsWithNextObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var c = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.MoveForward(a);

        Assert.Equal(new[] { b, a, c }, DataBandObjectNames(service));
    }

    [Fact]
    public void MoveForward_AtFront_IsClampedAndStaysLast()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.MoveForward(b); // b уже последний — дальше двигать некуда

        Assert.Equal(new[] { a, b }, DataBandObjectNames(service));
    }

    [Fact]
    public void MoveBackward_SwapsWithPreviousObject()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var c = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.MoveBackward(c);

        Assert.Equal(new[] { a, c, b }, DataBandObjectNames(service));
    }

    [Fact]
    public void MoveBackward_AtBack_IsClampedAndStaysFirst()
    {
        var service = new FastReportService();
        service.CreateNew();
        var a = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);
        var b = service.AddObject(DesignObjectType.Shape, 0, 0, 2, 2);

        service.MoveBackward(a); // a уже первый — дальше двигать некуда

        Assert.Equal(new[] { a, b }, DataBandObjectNames(service));
    }
}
