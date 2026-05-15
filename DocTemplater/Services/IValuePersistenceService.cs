using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>
/// Интерфейс сервиса сохранения значений
/// </summary>
public interface IValuePersistenceService
{
    /// <summary>
    /// Загрузить настройки приложения
    /// </summary>
    Task<AppSettings> LoadSettingsAsync();
    
    /// <summary>
    /// Сохранить настройки приложения
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);
    
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
    Task SaveTemplateValuesAsync(string templateFileName, Dictionary<string, object> values, string? lastSaveDirectory = null);
}
