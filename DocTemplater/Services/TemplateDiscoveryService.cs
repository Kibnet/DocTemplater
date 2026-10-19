using DocTemplater.Domain;
using Microsoft.Extensions.Logging;

namespace DocTemplater.Services;

/// <summary>
/// Сервис обнаружения и перечисления DOCX файлов шаблонов
/// </summary>
public class TemplateDiscoveryService : ITemplateDiscoveryService, IDisposable
{
    private string _templatesFolderPath;
    private readonly ILogger<TemplateDiscoveryService>? _logger;
    private FileSystemWatcher? _fileWatcher;

    public string TemplatesDirectory => _templatesFolderPath;

    /// <summary>
    /// Событие изменения списка шаблонов
    /// </summary>
    public event EventHandler? TemplatesChanged;

    public TemplateDiscoveryService(string templatesFolderPath, ILogger<TemplateDiscoveryService>? logger = null)
    {
        _templatesFolderPath = Path.GetFullPath(templatesFolderPath);
        _logger = logger;
        
        // Инициализировать FileSystemWatcher
        InitializeFileWatcher();
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

    /// <summary>
    /// Инициализировать FileSystemWatcher для отслеживания изменений
    /// </summary>
    private void InitializeFileWatcher()
    {
        try
        {
            // Создать папку, если не существует
            if (!Directory.Exists(_templatesFolderPath))
            {
                Directory.CreateDirectory(_templatesFolderPath);
            }

            _fileWatcher = new FileSystemWatcher(_templatesFolderPath)
            {
                Filter = "*.docx",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            // Подписаться на события
            _fileWatcher.Created += OnFileSystemChanged;
            _fileWatcher.Deleted += OnFileSystemChanged;
            _fileWatcher.Changed += OnFileSystemChanged;
            _fileWatcher.Renamed += OnFileSystemChanged;

            _logger?.LogInformation("Запущен мониторинг директории шаблонов: {Path}", _templatesFolderPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при инициализации FileSystemWatcher");
        }
    }

    /// <summary>
    /// Обработчик изменений файловой системы
    /// </summary>
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // Игнорировать временные файлы Word
        if (Path.GetFileName(e.FullPath).StartsWith("~$"))
            return;

        _logger?.LogDebug("Изменение в директории шаблонов: {ChangeType} - {Name}", e.ChangeType, e.Name);
        
        // Вызвать событие изменения
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Освободить ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    /// <summary>
    /// Установить новую директорию для шаблонов
    /// </summary>
    public void SetTemplatesDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (_templatesFolderPath == path)
            return;

        _templatesFolderPath = path;
        
        // Переинициализировать FileSystemWatcher
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
        
        InitializeFileWatcher();
        
        // Уведомить об изменении
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }
}
