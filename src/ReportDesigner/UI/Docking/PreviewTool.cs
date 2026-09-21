using Dock.Model.Mvvm.Controls;
using Newtonsoft.Json;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

/// <summary>
/// Обёртка над превью документа. В отличие от остальных панелей видимость управляется не
/// стандартным крестиком Dock (CanClose), а чекбоксом «Вид → Панель превью»
/// (<see cref="MainViewModel.IsPreviewVisible"/>) — единственный источник истины, чтобы не
/// расходиться с крестиком закрытия панели.
/// </summary>
public sealed class PreviewTool : Tool
{
    /// <summary>Не сериализуется вместе с раскладкой — после загрузки привязывается заново
    /// (<see cref="MainDockFactory.RestoreLayout"/>).</summary>
    [JsonIgnore]
    public MainViewModel ViewModel { get; set; } = null!;

    /// <summary>Для десериализатора раскладки.</summary>
    public PreviewTool()
    {
        Init();
    }

    public PreviewTool(MainViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    private void Init()
    {
        Id = "Preview";
        Title = "Превью";
        CanClose = false;
    }
}
