using Dock.Model.Mvvm.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Docking;

public sealed class ObjectTreeTool : Tool
{
    public ObjectTreeViewModel ViewModel { get; }

    public ObjectTreeTool(ObjectTreeViewModel viewModel)
    {
        ViewModel = viewModel;
        Id = "ObjectTree";
        Title = "Дерево объектов";
        CanClose = false;
    }
}
