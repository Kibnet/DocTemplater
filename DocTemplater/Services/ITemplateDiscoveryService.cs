using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>
/// Интерфейс сервиса обнаружения шаблонов
/// </summary>
public interface ITemplateDiscoveryService
{
    /// <summary>
    /// Получить все доступные шаблоны из папки Templates
    /// </summary>
    /// <returns>Коллекция метаданных шаблонов</returns>
    Task<List<Template>> DiscoverTemplatesAsync();
    
    /// <summary>
    /// Обновить список шаблонов
    /// </summary>
    /// <returns>Обновленная коллекция шаблонов</returns>
    Task<List<Template>> RefreshTemplatesAsync();
}
