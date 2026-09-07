using ReportDesigner.Services;
using Xunit;

namespace ReportDesigner.Tests;

public class RulerTickCalculatorTests
{
    [Fact]
    public void GetTicks_AtZoom1_StartsFromZero()
    {
        var ticks = RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 0, endCm: 5);

        Assert.NotEmpty(ticks);
        Assert.Equal(0f, ticks[0].ValueCm);
        Assert.True(ticks[0].IsMajor);
    }

    [Fact]
    public void GetTicks_PositionsMatchUnitConverter()
    {
        var ticks = RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 0, endCm: 3);

        foreach (var tick in ticks)
            Assert.Equal(UnitConverter.CmToPx(tick.ValueCm), tick.PositionPx, 2);
    }

    [Fact]
    public void GetTicks_CoversRequestedRange()
    {
        var ticks = RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 2, endCm: 8);

        Assert.True(ticks[0].ValueCm <= 2);
        Assert.True(ticks[^1].ValueCm >= 8 - 1); // последнее деление может не дотягивать до endCm ровно на шаг
        Assert.All(ticks, t => Assert.True(t.ValueCm >= 0));
    }

    [Fact]
    public void GetTicks_LowZoom_UsesBiggerStepThanHighZoom()
    {
        var lowZoomTicks = RulerTickCalculator.GetTicks(zoom: 0.25, startCm: 0, endCm: 100);
        var highZoomTicks = RulerTickCalculator.GetTicks(zoom: 4.0, startCm: 0, endCm: 100);

        // При мелком масштабе на тот же диапазон должно приходиться меньше делений — иначе они
        // сольются в кашу на экране.
        Assert.True(lowZoomTicks.Count < highZoomTicks.Count);
    }

    [Fact]
    public void GetTicks_NeverProducesNegativeValues()
    {
        var ticks = RulerTickCalculator.GetTicks(zoom: 1.0, startCm: -5, endCm: 3);

        Assert.All(ticks, t => Assert.True(t.ValueCm >= 0));
    }

    [Fact]
    public void GetTicks_EmptyWhenRangeInvalid()
    {
        Assert.Empty(RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 5, endCm: 5));
        Assert.Empty(RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 5, endCm: 2));
        Assert.Empty(RulerTickCalculator.GetTicks(zoom: 0, startCm: 0, endCm: 10));
    }

    [Fact]
    public void GetTicks_MajorTicksAreEvenlySpacedByIndex()
    {
        var ticks = RulerTickCalculator.GetTicks(zoom: 1.0, startCm: 0, endCm: 20);
        var majorValues = ticks.Where(t => t.IsMajor).Select(t => t.ValueCm).ToList();

        Assert.True(majorValues.Count >= 2);
        var step = majorValues[1] - majorValues[0];
        for (var i = 2; i < majorValues.Count; i++)
            Assert.Equal(step, majorValues[i] - majorValues[i - 1], 3);
    }
}
