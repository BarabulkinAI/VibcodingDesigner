using Dock.Model.Mvvm.Controls;
using Newtonsoft.Json;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class DataSourcesTool : Tool
{
    /// <summary>Не сериализуется вместе с раскладкой — после загрузки привязывается заново
    /// (<see cref="MainDockFactory.RestoreLayout"/>).</summary>
    [JsonIgnore]
    public DataSourcesViewModel ViewModel { get; set; } = null!;

    /// <summary>Для десериализатора раскладки.</summary>
    public DataSourcesTool()
    {
        Init();
    }

    public DataSourcesTool(DataSourcesViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    private void Init()
    {
        Id = "DataSources";
        Title = "Источники данных";
        CanClose = false;
    }
}
