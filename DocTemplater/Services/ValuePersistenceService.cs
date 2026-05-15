using System.Text.Json;
using DocTemplater.Domain;
using Microsoft.Extensions.Logging;

namespace DocTemplater.Services;

/// <summary>
/// Сервис сохранения и загрузки значений полей для каждого шаблона
/// </summary>
public class ValuePersistenceService : IValuePersistenceService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<ValuePersistenceService>? _logger;
    private AppSettings? _cachedSettings;

    public ValuePersistenceService(string? settingsFilePath = null, ILogger<ValuePersistenceService>? logger = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DocTemplater",
            "settings.json"
        );
        _logger = logger;
    }

    /// <summary>
    /// Загрузить настройки приложения
    /// </summary>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger?.LogInformation("Файл настроек не найден, создание по умолчанию");
                _cachedSettings = new AppSettings();
                await SaveSettingsAsync(_cachedSettings);
                return _cachedSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            
            _logger?.LogInformation("Настройки загружены из {Path}", _settingsFilePath);
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке настроек из {Path}", _settingsFilePath);
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
    }

    /// <summary>
    /// Сохранить настройки приложения
    /// </summary>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            // Создать директорию, если не существует
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            
            _cachedSettings = settings;
            _logger?.LogInformation("Настройки сохранены в {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при сохранении настроек в {Path}", _settingsFilePath);
            throw;
        }
    }

    /// <summary>
    /// Получить сохраненные значения для шаблона
    /// </summary>
    public async Task<SavedTemplateData?> GetSavedValuesAsync(string templateFileName)
    {
        var settings = await LoadSettingsAsync();
        var savedData = settings.Templates.FirstOrDefault(t => t.TemplateFileName == templateFileName);
        
        if (savedData != null)
        {
            _logger?.LogDebug("Найдены сохраненные значения для шаблона: {TemplateName}", templateFileName);
        }
        
        return savedData;
    }

    /// <summary>
    /// Сохранить значения для шаблона
    /// </summary>
    public async Task SaveTemplateValuesAsync(string templateFileName, Dictionary<string, object> values, string? lastSaveDirectory = null)
    {
        try
        {
            var settings = await LoadSettingsAsync();
            
            // Найти существующую запись или создать новую
            var existingData = settings.Templates.FirstOrDefault(t => t.TemplateFileName == templateFileName);
            
            if (existingData != null)
            {
                existingData.FieldValues = values;
                existingData.LastUsed = DateTimeOffset.Now;
                if (lastSaveDirectory != null)
                {
                    existingData.LastSaveDirectory = lastSaveDirectory;
                }
            }
            else
            {
                settings.Templates.Add(new SavedTemplateData
                {
                    TemplateFileName = templateFileName,
                    FieldValues = values,
                    LastUsed = DateTimeOffset.Now,
                    LastSaveDirectory = lastSaveDirectory
                });
            }

            await SaveSettingsAsync(settings);
            _logger?.LogInformation("Значения сохранены для шаблона: {TemplateName}", templateFileName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при сохранении значений для шаблона: {TemplateName}", templateFileName);
            throw;
        }
    }
}
