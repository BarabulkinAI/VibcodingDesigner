namespace ReportDesigner.Services;

/// <summary>Хранилище сериализованной раскладки Dock-панелей между запусками приложения.
/// Формат содержимого — забота вызывающего (см. <c>MainDockFactory</c>), хранилище работает с
/// готовой строкой.</summary>
public interface IDockLayoutStore
{
    /// <summary>Возвращает сохранённую раскладку либо null, если её нет или файл недоступен.</summary>
    string? Load();

    /// <summary>Сохраняет раскладку; ошибки записи не пробрасываются — потеря раскладки не должна
    /// мешать закрытию приложения.</summary>
    void Save(string content);
}
