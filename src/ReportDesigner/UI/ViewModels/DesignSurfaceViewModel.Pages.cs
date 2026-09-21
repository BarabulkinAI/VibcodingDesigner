using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Models;

namespace ReportDesigner.UI.ViewModels;

/// <summary>Вкладка страницы над канвасом. <see cref="Name"/> — внутреннее имя страницы в отчёте,
/// <see cref="Title"/> — подпись для пользователя («Стр. N» по порядку).</summary>
public sealed record PageTab(string Name, string Title, bool IsActive);

// Вкладки страниц: канвас и дерево объектов показывают одну активную страницу, переключение
// между ними — здесь. Само «какая страница активна» хранит IFastReportService (на ней работают
// AddBand/SetPageSize), эта часть VM только зеркалит это в снимок и вкладки.
public partial class DesignSurfaceViewModel
{
    [ObservableProperty] public partial IReadOnlyList<PageTab> PageTabs { get; set; } = Array.Empty<PageTab>();

    /// <summary>Сработало при смене активной страницы вкладкой. Это не изменение документа
    /// (<see cref="DocumentChanged"/> не вызывается — иначе на каждый клик по вкладке гонялся бы
    /// <c>Report.Prepare()</c> для превью); на него подписано дерево объектов.</summary>
    public event Action? ActivePageChanged;

    partial void OnSnapshotChanged(DesignSnapshot value)
    {
        PageTabs = value.Pages
            .Select((p, i) => new PageTab(p.Name, $"Стр. {i + 1}", p.Name == value.ActivePage?.Name))
            .ToList();
        RemovePageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectPage(string name)
    {
        if (name == Snapshot.ActivePage?.Name) return;

        _service.SetActivePage(name);
        ResetSelectionForPageChange();
        Snapshot = _service.GetSnapshot();
        ActivePageChanged?.Invoke();
    }

    [RelayCommand]
    private void AddPage()
    {
        _service.AddPage();
        ResetSelectionForPageChange();
        CommitChange();
    }

    [RelayCommand(CanExecute = nameof(CanRemovePage))]
    private void RemovePage()
    {
        if (Snapshot.ActivePage is not { } page) return;

        _service.RemovePage(page.Name);
        ResetSelectionForPageChange();
        CommitChange();
    }

    private bool CanRemovePage() => Snapshot.Pages.Count > 1;

    /// <summary>Выделение (объект/полоса) и жест относились к прежней странице.</summary>
    private void ResetSelectionForPageChange()
    {
        ClearGestureState();
        SelectedObjectName = null;
        SelectedBandName = null;
    }
}
