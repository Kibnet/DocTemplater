using DocTemplater.Domain;
using Microsoft.Extensions.Logging;

namespace DocTemplater.Services;

/// <summary>
/// Сервис обнаружения и перечисления DOCX файлов шаблонов
/// </summary>
public class TemplateDiscoveryService : ITemplateDiscoveryService
{
    private readonly string _templatesFolderPath;
    private readonly ILogger<TemplateDiscoveryService>? _logger;

    public TemplateDiscoveryService(string templatesFolderPath, ILogger<TemplateDiscoveryService>? logger = null)
    {
        _templatesFolderPath = templatesFolderPath;
        _logger = logger;
    }

    /// <summary>
    /// Получить все доступные шаблоны из папки Templates
    /// </summary>
    public async Task<List<Template>> DiscoverTemplatesAsync()
    {
        return await Task.Run(() => DiscoverTemplates());
    }

    /// <summary>
    /// Обновить список шаблонов
    /// </summary>
    public async Task<List<Template>> RefreshTemplatesAsync()
    {
        return await DiscoverTemplatesAsync();
    }

    private List<Template> DiscoverTemplates()
    {
        var templates = new List<Template>();

        try
        {
            // Создать папку Templates, если не существует
            if (!Directory.Exists(_templatesFolderPath))
            {
                _logger?.LogInformation("Папка Templates не найдена. Создание: {Path}", _templatesFolderPath);
                Directory.CreateDirectory(_templatesFolderPath);
                return templates;
            }

            // Сканировать папку на наличие .docx файлов
            var docxFiles = Directory.GetFiles(_templatesFolderPath, "*.docx", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("~$")) // Исключить временные файлы Word
                .OrderBy(f => Path.GetFileName(f));

            foreach (var filePath in docxFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var template = new Template
                    {
                        TemplateId = Guid.NewGuid(),
                        FileName = Path.GetFileNameWithoutExtension(fileInfo.Name),
                        FilePath = fileInfo.FullName,
                        LastModified = fileInfo.LastWriteTime
                    };
                    
                    templates.Add(template);
                    _logger?.LogDebug("Обнаружен шаблон: {FileName}", template.FileName);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Ошибка при обработке файла: {FilePath}", filePath);
                }
            }

            _logger?.LogInformation("Обнаружено {Count} шаблонов", templates.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при сканировании папки Templates: {Path}", _templatesFolderPath);
        }

        return templates;
    }
}
