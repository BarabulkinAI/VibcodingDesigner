using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class UnitConverterTests
{
    [Fact]
    public void CmToPx_ConvertsInchAndA4()
    {
        Assert.Equal(96, UnitConverter.CmToPx(UnitConverter.CmPerInch), 2);
        Assert.InRange(UnitConverter.CmToPx(21f), 793f, 794f);   // ширина A4
        Assert.InRange(UnitConverter.CmToPx(29.7f), 1118f, 1119f); // высота A4
    }

    [Fact]
    public void PxToCm_ConvertsBack()
    {
        Assert.Equal(2.54, UnitConverter.PxToCm(96f), 2);
        Assert.Equal(21, UnitConverter.PxToCm(UnitConverter.CmToPx(21f)), 2);
    }

    [Fact]
    public void MmToPx_And_PxToMm_AreConsistent()
    {
        Assert.Equal(UnitConverter.CmToPx(2.1f), UnitConverter.MmToPx(21f), 3);
        Assert.Equal(21, UnitConverter.PxToMm(UnitConverter.MmToPx(21f)), 3);
    }

    [Fact]
    public void RoundTrip_PreservesPrecision()
    {
        foreach (var cm in new[] { 0.1f, 1f, 5f, 21f, 29.7f, 3.54f })
            Assert.Equal(cm, UnitConverter.PxToCm(UnitConverter.CmToPx(cm)), 4);
    }
}