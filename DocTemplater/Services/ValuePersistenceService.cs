using System.Text.Json;
using System.Threading;
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
    private readonly SemaphoreSlim _storageLock = new(1, 1);

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
    /// Загрузить изолированный снимок настроек приложения
    /// </summary>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        await _storageLock.WaitAsync();
        try
        {
            return Snapshot(await LoadSettingsCoreAsync());
        }
        finally
        {
            _storageLock.Release();
        }
    }

    private async Task<AppSettings> LoadSettingsCoreAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger?.LogInformation("Файл настроек не найден, создание по умолчанию");
                await WriteSettingsCoreAsync(new AppSettings { TemplatesFolderPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_settingsFilePath))!, "Templates") });
                return _cachedSettings!;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json)
                ?? throw new InvalidDataException("Файл настроек содержит null.");
            if (_cachedSettings.Templates == null || _cachedSettings.Templates.Any(t => t.FieldValues == null))
                throw new InvalidDataException("Повреждена структура настроек.");
            
            _logger?.LogInformation("Настройки загружены из {Path}", _settingsFilePath);
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке настроек из {Path}", _settingsFilePath);
            _cachedSettings = null;
            throw new InvalidDataException($"Не удалось прочитать настройки {_settingsFilePath}. Исходный файл сохранён. Восстановите его из резервной копии и перезапустите приложение.", ex);
        }
    }

    /// <summary>
    /// Полностью заменить настройки переданным снимком
    /// </summary>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var snapshot = Snapshot(settings);
        await _storageLock.WaitAsync();
        try
        {
            await WriteSettingsCoreAsync(snapshot);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    public async Task SaveTemplatesDirectoryAsync(string directory)
    {
        await _storageLock.WaitAsync();
        try
        {
            var settings = Snapshot(await LoadSettingsCoreAsync());
            settings.TemplatesFolderPath = directory;
            await WriteSettingsCoreAsync(settings);
        }
        finally
        {
            _storageLock.Release();
        }
    }

    // Core-методы вызываются только под общим lock: снимок и запись — одна операция.
    private async Task WriteSettingsCoreAsync(AppSettings settings)
    {
        try
        {
            // Создать директорию, если не существует
            var fullPath = Path.GetFullPath(_settingsFilePath);
            var directory = Path.GetDirectoryName(fullPath);
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
            // Замена в том же каталоге сохраняет прежний JSON при отказе записи.
            var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, json);
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            
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
    public async Task SaveTemplateValuesAsync(
        string templateFileName,
        Dictionary<string, object> values,
        string? lastSaveDirectory = null,
        string? outputFileNameTemplate = null)
    {
        var snapshot = Snapshot(values);
        await _storageLock.WaitAsync();
        try
        {
            var settings = Snapshot(await LoadSettingsCoreAsync());
            
            // Найти существующую запись или создать новую
            var existingData = settings.Templates.FirstOrDefault(t => t.TemplateFileName == templateFileName);
            
            if (existingData != null)
            {
                existingData.FieldValues = snapshot;
                existingData.LastUsed = DateTimeOffset.Now;
                if (lastSaveDirectory != null)
                {
                    existingData.LastSaveDirectory = lastSaveDirectory;
                }

                if (outputFileNameTemplate != null)
                {
                    existingData.OutputFileNameTemplate = outputFileNameTemplate;
                }
            }
            else
            {
                settings.Templates.Add(new SavedTemplateData
                {
                    TemplateFileName = templateFileName,
                    FieldValues = snapshot,
                    LastUsed = DateTimeOffset.Now,
                    LastSaveDirectory = lastSaveDirectory,
                    OutputFileNameTemplate = outputFileNameTemplate
                });
            }

            await WriteSettingsCoreAsync(settings);
            _logger?.LogInformation("Значения сохранены для шаблона: {TemplateName}", templateFileName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при сохранении значений для шаблона: {TemplateName}", templateFileName);
            throw;
        }
        finally
        {
            _storageLock.Release();
        }
    }

    private static AppSettings Snapshot(AppSettings settings) => new()
    {
        TemplatesFolderPath = settings.TemplatesFolderPath,
        DefaultOutputPath = settings.DefaultOutputPath,
        CultureName = settings.CultureName,
        OpenAfterSaveDefault = settings.OpenAfterSaveDefault,
        Templates = settings.Templates.Select(template => new SavedTemplateData
        {
            TemplateFileName = template.TemplateFileName,
            FieldValues = Snapshot(template.FieldValues),
            LastUsed = template.LastUsed,
            LastSaveDirectory = template.LastSaveDirectory,
            OutputFileNameTemplate = template.OutputFileNameTemplate
        }).ToList()
    };

    private static Dictionary<string, object> Snapshot(Dictionary<string, object> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value switch
        {
            // Поддерживаемые скаляры неизменяемы; сохраняем их CLR-типы для текущего сеанса.
            string or int or DateTime or DateTimeOffset => pair.Value,
            _ => JsonSerializer.SerializeToElement(pair.Value)
        });
}
