using System.Text.Json;

namespace ReportDesigner.Services;

/// <summary>Хранит список недавних файлов в JSON рядом с пользовательскими настройками
/// (%AppData%/ReportDesigner/recent.json по умолчанию). Путь к файлу передаётся явно через
/// конструктор (а не читается из окружения внутри класса), чтобы тесты могли указать временный
/// путь без побочных эффектов на реальные настройки пользователя.</summary>
public class RecentFilesService : IRecentFilesService
{
    private const int MaxEntries = 8;
    private readonly string _filePath;

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReportDesigner", "recent.json");

    public RecentFilesService(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<string> GetRecent() => Load();

    public void Touch(string path)
    {
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxEntries)
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        Save(list);
    }

    public void Remove(string path)
    {
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Save(list);
    }

    private List<string> Load()
    {
        if (!File.Exists(_filePath)) return new List<string>();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Повреждённый/недоступный файл настроек не должен ронять приложение — просто
            // считаем, что недавних файлов нет.
            return new List<string>();
        }
    }

    private void Save(List<string> list)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(list));
    }
}
