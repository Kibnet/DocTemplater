using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>
/// Интерфейс сервиса генерации документов
/// </summary>
public interface IDocumentGenerationService
{
    /// <summary>
    /// Сгенерировать документ, заполнив шаблон значениями
    /// </summary>
    /// <param name="templatePath">Путь к файлу шаблона</param>
    /// <param name="outputPath">Путь для сохранения результата</param>
    /// <param name="values">Значения для подстановки</param>
    /// <param name="cultureName">Название культуры для форматирования</param>
    /// <returns>True если успешно</returns>
    Task<bool> GenerateDocumentAsync(string templatePath, string outputPath, 
        Dictionary<string, PlaceholderValue> values, string cultureName);
}
