using System.Globalization;
using System.Text.RegularExpressions;
using DocTemplater.Domain;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace DocTemplater.Services;

/// <summary>
/// Сервис генерации документов путем замены плейсхолдеров
/// </summary>
public class DocumentGenerationService : IDocumentGenerationService
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{(?<name>[^:}]+):(?<type>String|Integer|DateTimeOffset)\((?<format>[^)]*)\)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly ILogger<DocumentGenerationService>? _logger;

    public DocumentGenerationService(ILogger<DocumentGenerationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Сгенерировать документ, заполнив шаблон значениями
    /// </summary>
    public async Task<bool> GenerateDocumentAsync(string templatePath, string outputPath,
        Dictionary<string, PlaceholderValue> values, string cultureName)
    {
        return await Task.Run(() => GenerateDocument(templatePath, outputPath, values, cultureName));
    }

    private bool GenerateDocument(string templatePath, string outputPath,
        Dictionary<string, PlaceholderValue> values, string cultureName)
    {
        try
        {
            // Получить культуру для форматирования
            var culture = CultureInfo.GetCultureInfo(cultureName);

            // Скопировать шаблон в выходной файл
            File.Copy(templatePath, outputPath, true);

            // Открыть скопированный файл для редактирования
            using var document = WordprocessingDocument.Open(outputPath, true);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                _logger?.LogError("Документ не содержит тела: {TemplatePath}", templatePath);
                return false;
            }

            // Обработать каждый параграф отдельно (плейсхолдеры могут быть разделены между Text элементами)
            var paragraphs = body.Descendants<Paragraph>().ToList();
            var totalReplacements = 0;

            foreach (var paragraph in paragraphs)
            {
                // Собрать весь текст параграфа
                var textElements = paragraph.Descendants<Text>().ToList();
                if (textElements.Count == 0)
                    continue;

                var paragraphText = string.Concat(textElements.Select(t => t.Text));
                var matches = PlaceholderRegex.Matches(paragraphText);

                if (matches.Count == 0)
                    continue;

                // Выполнить замены в тексте параграфа
                var replacedText = paragraphText;
                foreach (Match match in matches)
                {
                    var placeholderName = match.Groups["name"].Value.Trim();
                    var format = match.Groups["format"].Value.Trim();

                    if (!values.TryGetValue(placeholderName, out var placeholderValue))
                    {
                        _logger?.LogWarning("Значение не найдено для плейсхолдера: {Name}", placeholderName);
                        continue;
                    }

                    // Форматировать значение
                    var formattedValue = FormatValue(placeholderValue.RawValue, format, culture);

                    // Заменить плейсхолдер форматированным значением
                    replacedText = replacedText.Replace(match.Value, formattedValue);
                    totalReplacements++;
                    
                    _logger?.LogDebug("Заменен плейсхолдер '{Placeholder}' на '{Value}'", match.Value, formattedValue);
                }

                // Если были замены, обновить параграф
                if (replacedText != paragraphText)
                {
                    // Удалить все существующие Text элементы
                    foreach (var textElement in textElements)
                    {
                        textElement.Remove();
                    }

                    // Создать новый Run с замененным текстом
                    var run = paragraph.GetFirstChild<Run>();
                    if (run == null)
                    {
                        run = new Run();
                        paragraph.AppendChild(run);
                    }
                    else
                    {
                        run.RemoveAllChildren<Text>();
                    }

                    run.AppendChild(new Text(replacedText));
                }
            }

            document.Save();
            
            _logger?.LogInformation("Документ успешно сгенерирован: {OutputPath}. Выполнено {Count} замен",
                outputPath, totalReplacements);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при генерации документа: {OutputPath}", outputPath);
            return false;
        }
    }

    /// <summary>
    /// Форматировать значение согласно строке формата и культуре
    /// </summary>
    private string FormatValue(object? value, string format, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        try
        {
            return value switch
            {
                DateTimeOffset dto => string.IsNullOrEmpty(format)
                    ? dto.ToString(culture)
                    : dto.ToString(format, culture),
                DateTime dt => string.IsNullOrEmpty(format)
                    ? dt.ToString(culture)
                    : dt.ToString(format, culture),
                int i => string.IsNullOrEmpty(format)
                    ? i.ToString(culture)
                    : i.ToString(format, culture),
                string s => s,
                _ => value.ToString() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при форматировании значения: {Value}, формат: {Format}", value, format);
            return value.ToString() ?? string.Empty;
        }
    }
}
