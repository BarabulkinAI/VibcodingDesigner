using Avalonia.Controls;

namespace ReportDesigner.Services;

/// <summary>
/// Доступ к главному окну приложения из сервисов, зарегистрированных в DI до того, как окно
/// создано (например, для файловых диалогов, которым нужен <see cref="TopLevel"/>).
/// </summary>
public interface IHostWindowProvider
{
    Window MainWindow { get; }
}
