using Avalonia.Controls;

namespace ReportDesigner.Services;

/// <summary>
/// Заполняется один раз сразу после создания <see cref="MainWindow"/> в
/// <c>App.OnFrameworkInitializationCompleted</c>.
/// </summary>
public class HostWindowProvider : IHostWindowProvider
{
    private Window? _mainWindow;

    public Window MainWindow => _mainWindow
        ?? throw new InvalidOperationException("Главное окно ещё не создано.");

    public void SetMainWindow(Window window) => _mainWindow = window;
}
