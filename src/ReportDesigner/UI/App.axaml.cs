using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;
using ReportDesigner.Views;
using MainViewModel = ReportDesigner.UI.ViewModels.MainViewModel;

namespace ReportDesigner;

public class App : Application
{
    private IServiceProvider _serviceProvider = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = BuildServiceProvider.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(_serviceProvider.GetRequiredService<MainViewModel>());
            _serviceProvider.GetRequiredService<HostWindowProvider>().SetMainWindow(window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}