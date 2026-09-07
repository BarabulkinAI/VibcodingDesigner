namespace ReportDesigner.Services;

/// <summary>Список недавно открытых/сохранённых файлов отчёта (*.frx), персистентный между
/// запусками приложения.</summary>
public interface IRecentFilesService
{
    /// <summary>Возвращает пути в порядке от самого недавнего к самому старому.</summary>
    IReadOnlyList<string> GetRecent();

    /// <summary>Помечает путь как только что открытый/сохранённый — перемещает в начало списка
    /// (или добавляет), убирая дубликаты и обрезая список до лимита.</summary>
    void Touch(string path);

    /// <summary>Убирает путь из списка (например, если файл не удалось открыть).</summary>
    void Remove(string path);
}
