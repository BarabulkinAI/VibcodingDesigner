using Microsoft.Extensions.DependencyInjection;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;
using ReportDesigner.Views;
using MainViewModel = ReportDesigner.UI.ViewModels.MainViewModel;

namespace ReportDesigner;

public static class BuildServiceProvider
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Регистрация сервисов
        services.AddSingleton<IFastReportService, FastReportService>();
        services.AddSingleton<IPreviewService, PreviewService>();
        services.AddSingleton<IExportService, ExportService>();
        // services.AddSingleton<IMyService, MyService>();
        // services.AddTransient<IMyTransientService, MyTransientService>();

        // Если есть окна/вьюмодели — регистрируйте их здесь
        
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }
}