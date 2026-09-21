namespace ReportDesigner.Services;

/// <summary>Хранит раскладку панелей в файле рядом с пользовательскими настройками
/// (%AppData%/ReportDesigner/layout.json по умолчанию). Путь передаётся явно — как и у
/// <see cref="RecentFilesService"/>, чтобы тесты могли использовать временный файл.</summary>
public class DockLayoutStore : IDockLayoutStore
{
    private readonly string _filePath;

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReportDesigner", "layout.json");

    public DockLayoutStore(string filePath)
    {
        _filePath = filePath;
    }

    public string? Load()
    {
        try
        {
            return File.Exists(_filePath) ? File.ReadAllText(_filePath) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_filePath, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не удалось сохранить раскладку — при следующем запуске будет раскладка по умолчанию.
        }
    }
}
