using System.Drawing;
using FastReport;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

public interface IFastReportService
{
    /// <summary>Текущий отчёт — единственный источник правды (FastReport Report).</summary>
    Report CurrentReport { get; }

    /// <summary>Путь текущего файла; null — документ «Новый» (ещё не сохранялся).</summary>
    string? CurrentFilePath { get; }

    /// <summary>Есть несохранённые изменения после последнего Load/Save.</summary>
    bool IsDirty { get; }

    void CreateNew();
    void Load(string path);
    void Save(string path);

    /// <summary>Загружает файл как шаблон нового документа: содержимое как у <see cref="Load"/>,
    /// но документ остаётся в состоянии «Новый» (без пути, без записи в недавние файлы) — первое
    /// сохранение станет Save As, как и после <see cref="CreateNew"/>.</summary>
    void LoadAsTemplate(string path);

    /// <summary>Фиксирует успешное сохранение: запоминает путь и сбрасывает признак изменённости.</summary>
    void MarkSaved(string path);

    /// <summary>Лёгкий снимок отчёта для отрисовки канваса и дерева объектов.</summary>
    DesignSnapshot GetSnapshot();

    // --- Страница ---
    /// <summary>Задаёт размер бумаги по пресету (A4/A3) и ориентацию. Не сбрасывает объекты —
    /// уже размещённые могут визуально выйти за пределы нового размера, если он меньше прежнего
    /// (осознанное ограничение, как и у ClampVertical при ресайзе полос).</summary>
    void SetPageSize(PageSizePreset preset, bool landscape);
    /// <summary>Текущий размер страницы как пресет — для предзаполнения диалога «Параметры
    /// страницы». Если размер не совпадает ни с одним пресетом (нестандартный сторонний файл),
    /// возвращает A4 с реальной ориентацией.</summary>
    (PageSizePreset Preset, bool Landscape) GetPageSize();

    // --- Полосы ---
    string AddBand(BandKind kind, float heightCm = 2f, string? bandName = null);
    void RemoveBand(string bandName);
    void RenameBand(string oldName, string newName);
    void SetBandHeight(string bandName, float heightCm);

    // --- Объекты ---
    string AddObject(DesignObjectType type, float leftCm, float topCm, float widthCm, float heightCm, string? bandName = null);
    void MoveObject(string objectName, float leftCm, float topCm);
    void ResizeObject(string objectName, float widthCm, float heightCm);
    void DeleteObject(string objectName);

    // --- Устаревшие методы (основные сценарии) ---
    void AddTextToDataBand(string text, float xCm, float yCm, float wCm, float hCm);

    // --- Свойства объектов ---
    void SetText(string objectName, string text);
    void SetFont(string objectName, string fontName, float fontSize, bool bold, bool italic);
    void SetTextColor(string objectName, Color color);
    void SetHorizontalAlign(string objectName, DesignTextAlign align);
    void SetVerticalAlign(string objectName, DesignVerticalAlign align);
    void SetBorder(string objectName, bool show, float widthCm, Color color);
    void SetFillColor(string objectName, Color color);
    void SetShapeKind(string objectName, DesignShapeKind kind);
    void SetLineStyle(string objectName, float widthCm, Color color);
    void SetImage(string objectName, string imagePath);
    void ClearImage(string objectName);
    void SetVisible(string objectName, bool visible);
    void SetName(string objectName, string newName);

    // --- Z-order ---
    void BringToFront(string objectName);
    void SendToBack(string objectName);
    void MoveForward(string objectName);
    void MoveBackward(string objectName);

    // --- Источники данных ---
    IReadOnlyList<string> GetDataSourceNames();
    IReadOnlyList<string> GetDataSourceColumns(string name);
    /// <summary>Создаёт источник данных с указанным именем или полностью заменяет его
    /// содержимое, если источник с таким именем уже есть.</summary>
    void SetDataSource(string name, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows);
    void RemoveDataSource(string name);
    void RenameDataSource(string oldName, string newName);
    /// <summary>Привязывает источник данных к полосе данных (<see cref="BandKind.Data"/>).
    /// <paramref name="dataSourceName"/> = null — отвязывает источник.</summary>
    void AssignBandDataSource(string bandName, string? dataSourceName);

    // --- Undo/redo ---
    bool CanUndo { get; }
    bool CanRedo { get; }
    /// <summary>Фиксирует точку в истории отмены — вызывается после каждой завершённой мутации
    /// документа (UI-слой вызывает это из своей единой точки синхронизации канваса).</summary>
    void Checkpoint();
    void Undo();
    void Redo();
}