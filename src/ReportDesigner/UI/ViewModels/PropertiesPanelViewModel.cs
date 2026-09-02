using System.ComponentModel;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportDesigner.Models;
using ReportDesigner.Services;
using ReportDesigner.ViewModels;

namespace ReportDesigner.UI.ViewModels;

/// <summary>
/// Панель свойств выделенного объекта: координаты/размер, текст, шрифт, выравнивание, цвета,
/// рамка. Мутирует документ напрямую через <see cref="IFastReportService"/> (как
/// <see cref="ObjectTreeViewModel"/>) и синхронизирует канвас через
/// <see cref="DesignSurfaceViewModel.CommitChange"/>.
///
/// Цвета редактируются как hex-строки (#RRGGBB) — простой TextBox без зависимости от
/// цветового пикера Avalonia.
///
/// Заполнение полей при смене выделения само вызывает генерируемые CommunityToolkit-ом
/// обработчики OnXxxChanged для каждого поля — без <see cref="_isRefreshing"/> это привело бы
/// к лишним/ошибочным вызовам сервиса и риску цикла через DocumentChanged (тот же класс бага,
/// что был найден и исправлен в DesignSurfaceViewModel на этапе 1).
/// </summary>
public partial class PropertiesPanelViewModel : ViewModelBase
{
    private readonly IFastReportService _service;
    private readonly DesignSurfaceViewModel _designSurface;
    private readonly IFilesService _filesService;
    private bool _isRefreshing;
    private string? _currentObjectName;

    [ObservableProperty] public partial bool HasSelection { get; set; }
    [ObservableProperty] public partial string SelectedTypeLabel { get; set; } = "";
    [ObservableProperty] public partial bool IsTextObject { get; set; }
    [ObservableProperty] public partial bool IsLineObject { get; set; }
    [ObservableProperty] public partial bool IsShapeObject { get; set; }
    [ObservableProperty] public partial bool IsPictureObject { get; set; }
    [ObservableProperty] public partial bool HasImage { get; set; }
    /// <summary>Рамка применима к Text и Shape (у Line своя рамка = стиль линии, у Picture рамки нет).</summary>
    [ObservableProperty] public partial bool IsBorderCapable { get; set; }

    [ObservableProperty] public partial double LeftCm { get; set; }
    [ObservableProperty] public partial double TopCm { get; set; }
    [ObservableProperty] public partial double WidthCm { get; set; }
    [ObservableProperty] public partial double HeightCm { get; set; }
    [ObservableProperty] public partial bool Visible { get; set; } = true;

    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial string FontName { get; set; } = "Arial";
    [ObservableProperty] public partial double FontSize { get; set; } = 10;
    [ObservableProperty] public partial bool FontBold { get; set; }
    [ObservableProperty] public partial bool FontItalic { get; set; }
    [ObservableProperty] public partial string TextColorHex { get; set; } = "#000000";
    [ObservableProperty] public partial DesignTextAlign HorizontalAlign { get; set; }
    [ObservableProperty] public partial DesignVerticalAlign VerticalAlign { get; set; }

    [ObservableProperty] public partial bool ShowBorder { get; set; }
    [ObservableProperty] public partial double BorderWidthCm { get; set; }
    [ObservableProperty] public partial string BorderColorHex { get; set; } = "#000000";

    [ObservableProperty] public partial string FillColorHex { get; set; } = "#FFFFFF";
    [ObservableProperty] public partial DesignShapeKind ShapeKind { get; set; }

    [ObservableProperty] public partial double LineWidthCm { get; set; }
    [ObservableProperty] public partial string LineColorHex { get; set; } = "#000000";

    public IReadOnlyList<DesignTextAlign> HorizontalAlignOptions { get; } = Enum.GetValues<DesignTextAlign>();
    public IReadOnlyList<DesignVerticalAlign> VerticalAlignOptions { get; } = Enum.GetValues<DesignVerticalAlign>();
    public IReadOnlyList<DesignShapeKind> ShapeKindOptions { get; } = Enum.GetValues<DesignShapeKind>();

    public PropertiesPanelViewModel(IFastReportService service, DesignSurfaceViewModel designSurface, IFilesService filesService)
    {
        _service = service;
        _designSurface = designSurface;
        _filesService = filesService;

        _designSurface.PropertyChanged += OnDesignSurfacePropertyChanged;
        _designSurface.DocumentChanged += RefreshFromSelection;

        RefreshFromSelection();
    }

    private void OnDesignSurfacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignSurfaceViewModel.SelectedObjectName))
            RefreshFromSelection();
    }

    private void RefreshFromSelection()
    {
        _isRefreshing = true;
        try
        {
            var found = _designSurface.FindSelectedObject();
            _currentObjectName = found?.Object.Name;
            HasSelection = found is not null;

            if (found is null)
            {
                IsTextObject = IsLineObject = IsShapeObject = IsPictureObject = IsBorderCapable = false;
                HasImage = false;
                SelectedTypeLabel = "";
                return;
            }

            var obj = found.Value.Object;
            SelectedTypeLabel = ObjectTypeLabel(obj.Type);
            IsTextObject = obj.Type == DesignObjectType.Text;
            IsLineObject = obj.Type == DesignObjectType.Line;
            IsShapeObject = obj.Type == DesignObjectType.Shape;
            IsPictureObject = obj.Type == DesignObjectType.Picture;
            IsBorderCapable = obj.Type is DesignObjectType.Text or DesignObjectType.Shape;
            HasImage = obj.HasImage;

            LeftCm = Math.Round(UnitConverter.PxToCm(obj.Bounds.X), 2);
            TopCm = Math.Round(UnitConverter.PxToCm(obj.Bounds.Y), 2);
            WidthCm = Math.Round(UnitConverter.PxToCm(obj.Bounds.Width), 2);
            HeightCm = Math.Round(UnitConverter.PxToCm(obj.Bounds.Height), 2);
            Visible = obj.Visible;

            Text = obj.Text ?? "";
            FontName = obj.FontName;
            FontSize = obj.FontSize;
            FontBold = obj.FontBold;
            FontItalic = obj.FontItalic;
            TextColorHex = ToHex(obj.TextColor);
            HorizontalAlign = obj.HorizontalAlign;
            VerticalAlign = obj.VerticalAlign;

            ShowBorder = obj.ShowBorder;
            BorderWidthCm = Math.Round(UnitConverter.PxToCm(obj.BorderWidth), 2);
            BorderColorHex = ToHex(obj.BorderColor);

            FillColorHex = ToHex(obj.FillColor);
            ShapeKind = obj.Shape;

            LineWidthCm = Math.Round(UnitConverter.PxToCm(obj.LineWidth), 2);
            LineColorHex = ToHex(obj.LineColor);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string ObjectTypeLabel(DesignObjectType type) => type switch
    {
        DesignObjectType.Text => "Текст",
        DesignObjectType.Line => "Линия",
        DesignObjectType.Shape => "Фигура",
        DesignObjectType.Picture => "Картинка",
        _ => type.ToString(),
    };

    // ------------------------------------------------------------------
    // Координаты / размер / видимость
    // ------------------------------------------------------------------

    partial void OnLeftCmChanged(double value) => ApplyBounds();
    partial void OnTopCmChanged(double value) => ApplyBounds();
    partial void OnWidthCmChanged(double value) => ApplyBounds();
    partial void OnHeightCmChanged(double value) => ApplyBounds();

    private void ApplyBounds()
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.MoveObject(name, (float)LeftCm, (float)TopCm);
        _service.ResizeObject(name, (float)WidthCm, (float)HeightCm);
        _designSurface.CommitChange();
    }

    partial void OnVisibleChanged(bool value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetVisible(name, value);
        _designSurface.CommitChange();
    }

    // ------------------------------------------------------------------
    // Текст / шрифт / выравнивание
    // ------------------------------------------------------------------

    partial void OnTextChanged(string value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetText(name, value);
        _designSurface.CommitChange();
    }

    partial void OnFontNameChanged(string value) => ApplyFont();
    partial void OnFontSizeChanged(double value) => ApplyFont();
    partial void OnFontBoldChanged(bool value) => ApplyFont();
    partial void OnFontItalicChanged(bool value) => ApplyFont();

    private void ApplyFont()
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetFont(name, FontName, (float)FontSize, FontBold, FontItalic);
        _designSurface.CommitChange();
    }

    partial void OnTextColorHexChanged(string value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        if (!TryParseHex(value, out var color)) return;
        _service.SetTextColor(name, color);
        _designSurface.CommitChange();
    }

    partial void OnHorizontalAlignChanged(DesignTextAlign value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetHorizontalAlign(name, value);
        _designSurface.CommitChange();
    }

    partial void OnVerticalAlignChanged(DesignVerticalAlign value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetVerticalAlign(name, value);
        _designSurface.CommitChange();
    }

    // ------------------------------------------------------------------
    // Рамка / заливка / фигура / линия
    // ------------------------------------------------------------------

    partial void OnShowBorderChanged(bool value) => ApplyBorder();
    partial void OnBorderWidthCmChanged(double value) => ApplyBorder();
    partial void OnBorderColorHexChanged(string value) => ApplyBorder();

    private void ApplyBorder()
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        if (!TryParseHex(BorderColorHex, out var color)) return;
        _service.SetBorder(name, ShowBorder, (float)BorderWidthCm, color);
        _designSurface.CommitChange();
    }

    partial void OnFillColorHexChanged(string value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        if (!TryParseHex(value, out var color)) return;
        _service.SetFillColor(name, color);
        _designSurface.CommitChange();
    }

    partial void OnShapeKindChanged(DesignShapeKind value)
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        _service.SetShapeKind(name, value);
        _designSurface.CommitChange();
    }

    partial void OnLineWidthCmChanged(double value) => ApplyLineStyle();
    partial void OnLineColorHexChanged(string value) => ApplyLineStyle();

    private void ApplyLineStyle()
    {
        if (_isRefreshing || _currentObjectName is not { } name) return;
        if (!TryParseHex(LineColorHex, out var color)) return;
        _service.SetLineStyle(name, (float)LineWidthCm, color);
        _designSurface.CommitChange();
    }

    // ------------------------------------------------------------------
    // Картинка
    // ------------------------------------------------------------------

    [RelayCommand]
    private async Task PickImageAsync()
    {
        if (_currentObjectName is not { } name) return;

        var path = await _filesService.PickImagePathAsync();
        if (path is null) return;

        _service.SetImage(name, path);
        HasImage = true;
        _designSurface.CommitChange();
    }

    [RelayCommand]
    private void ClearImage()
    {
        if (_currentObjectName is not { } name) return;

        _service.ClearImage(name);
        HasImage = false;
        _designSurface.CommitChange();
    }

    // ------------------------------------------------------------------
    // Hex-цвета
    // ------------------------------------------------------------------

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Color.Black;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var s = hex.TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var value))
            return false;
        color = Color.FromArgb((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        return true;
    }
}
