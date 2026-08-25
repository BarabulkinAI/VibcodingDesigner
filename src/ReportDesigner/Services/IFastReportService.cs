using FastReport;
using ReportDesigner.Models;

namespace ReportDesigner.Services;

public interface IFastReportService
{
    /// <summary>Текущий отчёт — единственный источник правды (FastReport Report).</summary>
    Report CurrentReport { get; }

    void CreateNew();
    void Load(string path);
    void Save(string path);

    /// <summary>Лёгкий снимок отчёта для отрисовки канваса и дерева объектов.</summary>
    DesignSnapshot GetSnapshot();

    // --- Полосы ---
    string AddBand(BandKind kind, float heightCm = 2f, string? bandName = null);
    void RemoveBand(string bandName);

    // --- Объекты ---
    string AddObject(DesignObjectType type, float leftCm, float topCm, float widthCm, float heightCm, string? bandName = null);
    void MoveObject(string objectName, float leftCm, float topCm);
    void ResizeObject(string objectName, float widthCm, float heightCm);
    void DeleteObject(string objectName);

    // --- Устаревшие методы (основные сценарии) ---
    void AddTextToDataBand(string text, float xCm, float yCm, float wCm, float hCm);
}