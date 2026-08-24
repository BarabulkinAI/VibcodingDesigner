namespace ReportDesigner.Services;

/// <summary>
/// Конвертация между сантиметрами и логическими пикселями (96 DPI).
/// Канвас дизайнера работает в логических пикселях, FastReport — в сантиметрах/юнитах.
/// </summary>
public static class UnitConverter
{
    public const float LogicalDpi = 96f;
    public const float CmPerInch = 2.54f;
    public const float MmPerCm = 10f;

    public static float CmToPx(float cm) => cm * LogicalDpi / CmPerInch;
    public static float PxToCm(float px) => px * CmPerInch / LogicalDpi;
    public static float MmToPx(float mm) => CmToPx(mm / MmPerCm);
    public static float PxToMm(float px) => PxToCm(px) * MmPerCm;
}