using Avalonia.Media.Imaging;
using FastReport;

namespace ReportDesigner.Services;

public interface IPreviewService
{
    Bitmap RenderPreview(Report report);
}