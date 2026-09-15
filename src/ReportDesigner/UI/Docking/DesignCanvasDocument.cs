using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class DesignCanvasDocument : Document
{
    public DesignSurfaceViewModel ViewModel { get; }

    public DesignCanvasDocument(DesignSurfaceViewModel viewModel)
    {
        ViewModel = viewModel;
        Id = "DesignCanvas";
        Title = "Канвас";
        CanClose = false;
    }
}
