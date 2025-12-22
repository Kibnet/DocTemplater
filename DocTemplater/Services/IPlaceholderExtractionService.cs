using DocTemplater.Domain;

namespace DocTemplater.Services;

/// <summary>
/// Интерфейс сервиса извлечения плейсхолдеров
/// </summary>
public interface IPlaceholderExtractionService
{
    /// <summary>
    /// Извлечь все плейсхолдеры из DOCX файла
    /// </summary>
    /// <param name="filePath">Путь к DOCX файлу</param>
    /// <returns>Список извлеченных плейсхолдеров</returns>
    Task<List<Placeholder>> ExtractPlaceholdersAsync(string filePath);
}
