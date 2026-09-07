namespace ReportDesigner.Services;

/// <summary>
/// Чистая логика расчёта делений линейки — без зависимости от Avalonia, чтобы оставаться
/// юнит-тестируемой напрямую (как ResizeGeometry/UnitConverter). Отрисовка (RulerView) только
/// переводит результат в экранные координаты и рисует.
/// </summary>
public static class RulerTickCalculator
{
    private static readonly float[] NiceStepsCm = { 0.5f, 1f, 2f, 5f, 10f, 20f, 50f, 100f };

    public readonly record struct Tick(float PositionPx, float ValueCm, bool IsMajor);

    /// <summary>
    /// Возвращает деления в системе координат страницы (см → px через UnitConverter, БЕЗ учёта
    /// zoom и экранного смещения — это забота вызывающего кода), покрывающие
    /// [startCm, endCm]. Шаг между делениями подбирается так, чтобы на экране при данном zoom
    /// между соседними делениями было не меньше minSpacingPx — иначе на мелком масштабе линейка
    /// превращается в сплошную чёрточную кашу.
    /// </summary>
    public static IReadOnlyList<Tick> GetTicks(double zoom, float startCm, float endCm, float minSpacingPx = 25f)
    {
        if (zoom <= 0 || endCm <= startCm) return Array.Empty<Tick>();

        var stepCm = NiceStepsCm[^1];
        foreach (var candidate in NiceStepsCm)
        {
            if (candidate * UnitConverter.CmToPx(1f) * zoom >= minSpacingPx)
            {
                stepCm = candidate;
                break;
            }
        }

        // Не подписываем каждое деление — крупное (подписанное) через одно, если шаг мелкий, и
        // через пять — иначе (совпадает со стандартной "1-2-5" прогрессией линеек).
        var majorEvery = stepCm < 1f ? 2 : 5;

        var ticks = new List<Tick>();
        var first = MathF.Max(0, MathF.Floor(startCm / stepCm) * stepCm);
        var index = (int)MathF.Round(first / stepCm);
        for (var value = first; value <= endCm; value += stepCm, index++)
        {
            ticks.Add(new Tick(UnitConverter.CmToPx(value), value, index % majorEvery == 0));
        }
        return ticks;
    }
}
