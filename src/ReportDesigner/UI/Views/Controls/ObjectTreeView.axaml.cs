using System.ComponentModel;
using Avalonia.Controls;
using ReportDesigner.UI.ViewModels;

namespace ReportDesigner.UI.Views.Controls;

/// <summary>
/// Дерево полос/объектов. Выделение синхронизируется вручную через код-behind в обе стороны
/// (а не через двустороннюю привязку TreeView.SelectedItem), потому что дерево показывает два
/// разных типа узлов (полосы и объекты) и <see cref="ObjectTreeViewModel.SelectedNode"/> должен
/// хранить только объектные — клик по узлу полосы просто снимает выделение.
/// </summary>
public partial class ObjectTreeView : UserControl
{
    private bool _syncingFromViewModel;
    private bool _syncingFromTree;

    public ObjectTreeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Tree.SelectionChanged += OnTreeSelectionChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ObjectTreeViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ObjectTreeViewModel.SelectedNode)) return;
        // Изменение SelectedNode пришло как следствие клика в самом дереве (см. OnTreeSelectionChanged
        // ниже) — не нужно синхронно писать обратно в Tree.SelectedItem изнутри того же
        // SelectionChanged, который это изменение и вызвал (реентрантная запись в состояние
        // выделения TreeView прямо во время его же обработки — именно так падало приложение при
        // клике на узел полосы: SelectedNode уходил в null и код пытался тут же обнулить
        // Tree.SelectedItem, ещё не выйдя из исходного SelectionChanged).
        if (_syncingFromTree) return;
        if (DataContext is not ObjectTreeViewModel vm) return;

        _syncingFromViewModel = true;
        Tree.SelectedItem = vm.SelectedNode;
        _syncingFromViewModel = false;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingFromViewModel) return;
        if (DataContext is not ObjectTreeViewModel vm) return;

        _syncingFromTree = true;
        vm.SelectedNode = Tree.SelectedItem as ObjectTreeObjectNode;
        _syncingFromTree = false;
    }
}
