using Dock.Model.Mvvm.Controls;
using Newtonsoft.Json;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class DesignCanvasDocument : Document
{
    /// <summary>Не сериализуется вместе с раскладкой — после загрузки привязывается заново
    /// (<see cref="MainDockFactory.RestoreLayout"/>).</summary>
    [JsonIgnore]
    public DesignSurfaceViewModel ViewModel { get; set; } = null!;

    /// <summary>Для десериализатора раскладки.</summary>
    public DesignCanvasDocument()
    {
        Init();
    }

    public DesignCanvasDocument(DesignSurfaceViewModel viewModel) : this()
    {
        ViewModel = viewModel;
    }

    private void Init()
    {
        Id = "DesignCanvas";
        Title = "Канвас";
        CanClose = false;
    }
}
