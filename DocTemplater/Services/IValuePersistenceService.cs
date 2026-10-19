using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>
/// Интерфейс сервиса сохранения значений
/// </summary>
public interface IValuePersistenceService
{
    /// <summary>
    /// Загрузить изолированный снимок настроек приложения
    /// </summary>
    Task<AppSettings> LoadSettingsAsync();
    
    /// <summary>
    /// Полностью заменить настройки переданным снимком (инициализация/восстановление).
    /// Для изменения отдельных данных используйте соответствующий метод обновления.
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Обновить только каталог шаблонов на актуальном состоянии настроек
    /// </summary>
    Task SaveTemplatesDirectoryAsync(string directory);
    
    /// <summary>
    /// Получить сохраненные значения для шаблона
    /// </summary>
    Task<SavedTemplateData?> GetSavedValuesAsync(string templateFileName);
    
    /// <summary>
    /// Сохранить значения для шаблона
    /// </summary>
    /// <param name="templateFileName">Имя файла шаблона</param>
    /// <param name="values">Значения полей</param>
    /// <param name="lastSaveDirectory">Последний каталог сохранения</param>
    /// <param name="outputFileNameTemplate">Шаблон имени выходного файла</param>
    Task SaveTemplateValuesAsync(
        string templateFileName,
        Dictionary<string, object> values,
        string? lastSaveDirectory = null,
        string? outputFileNameTemplate = null);
}
