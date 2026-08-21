
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFastReportService _fastReportService;
    private readonly IPreviewService _previewService;
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
    public Bitmap? PreviewImage { get; private set; }
    public event Action? PreviewChanged;

    public MainViewModel(IFastReportService  fastReportService, IPreviewService previewService)
    {
        _fastReportService = fastReportService;
        _previewService = previewService;
        
        _fastReportService.CreateNew();
        _fastReportService.AddTextToDataBand("Привет, FastReport!", 1, 1, 8, 1);
        
        RefreshPreview();
    }
    public void RefreshPreview()
    {
        PreviewImage = _previewService.RenderPreview(_fastReportService.CurrentReport);
        PreviewChanged?.Invoke();
    }
}