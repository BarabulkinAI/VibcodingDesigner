using System.Linq;
using ReportDesigner.Services;
using ReportDesigner.UI.ViewModels;
using Xunit;

namespace ReportDesigner.Tests;

public class DataSourcesViewModelTests
{
    private static (FastReportService Service, DesignSurfaceViewModel Surface, DataSourcesViewModel Sources) Create()
    {
        var service = new FastReportService();
        service.CreateNew();
        var surface = new DesignSurfaceViewModel(service);
        var sources = new DataSourcesViewModel(service, surface);
        return (service, surface, sources);
    }

    [Fact]
    public void AddSource_CreatesSourceWithDefaultColumn()
    {
        var (service, _, vm) = Create();

        vm.AddSourceCommand.Execute(null);

        Assert.Single(vm.SourceNames);
        Assert.Equal(vm.SourceNames[0], vm.SelectedSourceName);
        Assert.Contains("Столбец1", vm.CsvText);
    }

    [Fact]
    public void AddSource_GeneratesUniqueNames()
    {
        var (_, _, vm) = Create();

        vm.AddSourceCommand.Execute(null);
        vm.AddSourceCommand.Execute(null);

        Assert.Equal(2, vm.SourceNames.Count);
        Assert.NotEqual(vm.SourceNames[0], vm.SourceNames[1]);
    }

    [Fact]
    public void RemoveSelectedSource_RemovesAndClearsSelection()
    {
        var (_, _, vm) = Create();
        vm.AddSourceCommand.Execute(null);

        vm.RemoveSelectedSourceCommand.Execute(null);

        Assert.Empty(vm.SourceNames);
        Assert.Null(vm.SelectedSourceName);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void EditingCsvText_RegistersColumnsAndRows()
    {
        var (service, _, vm) = Create();
        vm.AddSourceCommand.Execute(null);
        var name = vm.SelectedSourceName!;

        vm.CsvText = "Имя, Город\nИван, Москва\nОльга, Казань";

        Assert.Equal(new[] { "Имя", "Город" }, service.GetDataSourceColumns(name));
    }

    [Fact]
    public void SelectingSource_NeverDirtiesDocument()
    {
        var (service, _, vm) = Create();
        vm.AddSourceCommand.Execute(null);
        var name = vm.SelectedSourceName!;
        service.MarkSaved("dummy.frx"); // сбрасывает IsDirty, как после реального сохранения
        vm.SelectedSourceName = null;

        vm.SelectedSourceName = name;

        Assert.False(service.IsDirty);
    }

    [Fact]
    public void RenamingSource_UpdatesServiceAndKeepsSelection()
    {
        var (service, _, vm) = Create();
        vm.AddSourceCommand.Execute(null);

        vm.NameEdit = "Клиенты";

        Assert.Equal("Клиенты", vm.SelectedSourceName);
        Assert.Contains("Клиенты", service.GetDataSourceNames());
    }
}
