using System.Text.RegularExpressions;
using DocTemplater.Domain;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace DocTemplater.Services;

/// <summary>
/// Сервис извлечения плейсхолдеров из DOCX документов
/// </summary>
public class PlaceholderExtractionService : IPlaceholderExtractionService
{
    // Паттерн: {НазваниеПлейсхолдера:Тип(СтрокаФормата)}
    private static readonly Regex PlaceholderRegex = new(
        @"\{(?<name>[^:}]+):(?<type>String|Integer|DateTimeOffset)\((?<format>[^)]*)\)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly ILogger<PlaceholderExtractionService>? _logger;

    public PlaceholderExtractionService(ILogger<PlaceholderExtractionService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Извлечь все плейсхолдеры из DOCX файла
    /// </summary>
    public async Task<List<Placeholder>> ExtractPlaceholdersAsync(string filePath)
    {
        return await Task.Run(() => ExtractPlaceholders(filePath));
    }

    private List<Placeholder> ExtractPlaceholders(string filePath)
    {
        var placeholderDict = new Dictionary<string, PlaceholderInfo>();

        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                _logger?.LogWarning("Документ не содержит тела: {FilePath}", filePath);
                return new List<Placeholder>();
            }

            // Извлечь весь текст из документа
            var text = body.InnerText;
            
            // Найти все совпадения плейсхолдеров
            var matches = PlaceholderRegex.Matches(text);
            
            _logger?.LogDebug("Найдено {Count} совпадений плейсхолдеров в файле: {FilePath}", matches.Count, filePath);

            foreach (Match match in matches)
            {
                try
                {
                    var name = match.Groups["name"].Value.Trim();
                    var typeStr = match.Groups["type"].Value.Trim();
                    var format = match.Groups["format"].Value.Trim();

                    // Преобразовать строку типа в enum
                    if (!Enum.TryParse<PlaceholderDataType>(typeStr, true, out var dataType))
                    {
                        _logger?.LogWarning("Неизвестный тип данных '{Type}' для плейсхолдера '{Name}'", typeStr, name);
                        continue;
                    }

                    // Добавить или обновить информацию о плейсхолдере
                    if (!placeholderDict.ContainsKey(name))
                    {
                        placeholderDict[name] = new PlaceholderInfo
                        {
                            Name = name,
                            DataType = dataType,
                            FormatPatterns = new HashSet<string>()
                        };
                    }

                    var info = placeholderDict[name];
                    
                    // Проверка согласованности типа
                    if (info.DataType != dataType)
                    {
                        _logger?.LogWarning(
                            "Несогласованность типа для плейсхолдера '{Name}': {Type1} vs {Type2}. Используется первый тип.",
                            name, info.DataType, dataType);
                    }

                    // Добавить уникальный формат
                    if (!string.IsNullOrEmpty(format))
                    {
                        info.FormatPatterns.Add(format);
                    }

                    info.OccurrenceCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Ошибка при разборе плейсхолдера: {Match}", match.Value);
                }
            }

            // Преобразовать в список Placeholder
            var placeholders = placeholderDict.Values.Select(info => new Placeholder
            {
                Name = info.Name,
                DataType = info.DataType,
                FormatPatterns = info.FormatPatterns.ToList(),
                OccurrenceCount = info.OccurrenceCount
            }).ToList();

            _logger?.LogInformation("Извлечено {Count} уникальных плейсхолдеров из {FilePath}", 
                placeholders.Count, Path.GetFileName(filePath));

            return placeholders;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при извлечении плейсхолдеров из файла: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Вспомогательный класс для накопления информации о плейсхолдерах
    /// </summary>
    private class PlaceholderInfo
    {
        public string Name { get; set; } = string.Empty;
        public PlaceholderDataType DataType { get; set; }
        public HashSet<string> FormatPatterns { get; set; } = new();
        public int OccurrenceCount { get; set; }
    }
}
