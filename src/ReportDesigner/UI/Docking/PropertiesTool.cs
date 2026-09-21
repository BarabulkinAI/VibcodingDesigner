using Dock.Model.Mvvm.Controls;
using Newtonsoft.Json;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class PropertiesTool : Tool
{
    /// <summary>Не сериализуется вместе с раскладкой — после загрузки привязывается заново
    /// (<see cref="MainDockFactory.RestoreLayout"/>).</summary>
    [JsonIgnore]
    public PropertiesPanelViewModel ViewModel { get; set; } = null!;

    /// <summary>Для десериализатора раскладки.</summary>
    public PropertiesTool()
    {
        Init();
    }

    public PropertiesTool(PropertiesPanelViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    private void Init()
    {
        Id = "Properties";
        Title = "Свойства";
        CanClose = false;
    }
}
