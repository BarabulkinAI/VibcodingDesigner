using Dock.Model.Mvvm.Controls;
using Newtonsoft.Json;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class ObjectTreeTool : Tool
{
    /// <summary>Не сериализуется вместе с раскладкой — после загрузки привязывается заново
    /// (<see cref="MainDockFactory.RestoreLayout"/>).</summary>
    [JsonIgnore]
    public ObjectTreeViewModel ViewModel { get; set; } = null!;

    /// <summary>Для десериализатора раскладки.</summary>
    public ObjectTreeTool()
    {
        Init();
    }

    public ObjectTreeTool(ObjectTreeViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    private void Init()
    {
        Id = "ObjectTree";
        Title = "Дерево объектов";
        CanClose = false;
    }
}
