using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using DocTemplater.Domain;
using DocTemplater.Services;
using ReactiveUI;

namespace DocTemplater.ViewModels;

/// <summary>
/// ViewModel для экрана ввода данных
/// </summary>
public class DataInputViewModel : ViewModelBase
{
    private readonly Template _template;
    private readonly IDocumentGenerationService _documentGenerationService;
    private readonly IValuePersistenceService _valuePersistenceService;
    private readonly CultureInfo _culture;
    
    private List<PlaceholderFieldViewModel> _fields = new();
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _outputFileName = string.Empty;
    private bool _openAfterSave = false;
    private bool _isSaving;
    private string _statusMessage = string.Empty;

    public DataInputViewModel(
        Template template,
        IDocumentGenerationService documentGenerationService,
        IValuePersistenceService valuePersistenceService,
        string cultureName = "ru-RU")
    {
        _template = template;
        _documentGenerationService = documentGenerationService;
        _valuePersistenceService = valuePersistenceService;
        _culture = CultureInfo.GetCultureInfo(cultureName);

        // Установить имя выходного файла по умолчанию
        OutputFileName = $"{template.FileName}_заполненный";

        // Команды
        SaveCommand = ReactiveCommand.CreateFromTask(SaveDocumentAsync);
        BackCommand = ReactiveCommand.Create(() => { });
        BrowseOutputDirectoryCommand = ReactiveCommand.Create(BrowseOutputDirectory);
        BrowseOutputPathCommand = ReactiveCommand.Create(BrowseOutputDirectory);
        ClearCommand = ReactiveCommand.Create(ClearFields);

        // Загрузить сохраненные значения и создать поля
        LoadSavedValuesCommand = ReactiveCommand.CreateFromTask(LoadSavedValuesAsync);
        LoadSavedValuesCommand.Execute().Subscribe();
    }

    /// <summary>
    /// Поля для ввода
    /// </summary>
    public List<PlaceholderFieldViewModel> Fields
    {
        get => _fields;
        set => this.RaiseAndSetIfChanged(ref _fields, value);
    }

    /// <summary>
    /// Каталог вывода
    /// </summary>
    public string OutputDirectory
    {
        get => _outputDirectory;
        set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
    }

    /// <summary>
    /// Имя выходного файла
    /// </summary>
    public string OutputFileName
    {
        get => _outputFileName;
        set => this.RaiseAndSetIfChanged(ref _outputFileName, value);
    }

    /// <summary>
    /// Открыть после сохранения
    /// </summary>
    public bool OpenAfterSave
    {
        get => _openAfterSave;
        set => this.RaiseAndSetIfChanged(ref _openAfterSave, value);
    }

    /// <summary>
    /// Флаг сохранения
    /// </summary>
    public bool IsSaving
    {
        get => _isSaving;
        set => this.RaiseAndSetIfChanged(ref _isSaving, value);
    }

    /// <summary>
    /// Сообщение о статусе
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>
    /// Имя шаблона
    /// </summary>
    public string TemplateName => _template.FileName;

    /// <summary>
    /// Полный путь к выходному файлу
    /// </summary>
    public string OutputFilePath
    {
        get => Path.Combine(OutputDirectory, OutputFileName + ".docx");
        set
        {
            var directory = Path.GetDirectoryName(value);
            var fileName = Path.GetFileNameWithoutExtension(value);
            if (!string.IsNullOrEmpty(directory))
                OutputDirectory = directory;
            if (!string.IsNullOrEmpty(fileName))
                OutputFileName = fileName;
        }
    }

    /// <summary>
    /// Команда сохранения
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>
    /// Команда возврата
    /// </summary>
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    /// <summary>
    /// Команда выбора каталога
    /// </summary>
    public ReactiveCommand<Unit, Unit> BrowseOutputDirectoryCommand { get; }

    /// <summary>
    /// Команда выбора пути к выходному файлу
    /// </summary>
    public ReactiveCommand<Unit, Unit> BrowseOutputPathCommand { get; }

    /// <summary>
    /// Команда очистки полей
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    /// <summary>
    /// Команда загрузки сохраненных значений
    /// </summary>
    public ReactiveCommand<Unit, Unit> LoadSavedValuesCommand { get; }

    /// <summary>
    /// Событие успешного сохранения
    /// </summary>
    public event EventHandler? DocumentSaved;

    private async Task LoadSavedValuesAsync()
    {
        try
        {
            // Получить сохраненные значения
            var savedData = await _valuePersistenceService.GetSavedValuesAsync(_template.FileName + ".docx");

            // Создать поля для каждого плейсхолдера
            var fields = new List<PlaceholderFieldViewModel>();
            
            foreach (var placeholder in _template.Placeholders)
            {
                object? savedValue = null;
                if (savedData?.FieldValues.TryGetValue(placeholder.Name, out var value) == true)
                {
                    savedValue = ConvertSavedValue(value, placeholder.DataType);
                }

                var field = new PlaceholderFieldViewModel(placeholder, _culture, savedValue);
                fields.Add(field);
            }

            Fields = fields;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при загрузке сохраненных значений: {ex.Message}";
        }
    }

    private object? ConvertSavedValue(object value, PlaceholderDataType dataType)
    {
        try
        {
            return dataType switch
            {
                PlaceholderDataType.Integer => Convert.ToInt32(value),
                PlaceholderDataType.DateTimeOffset => value is DateTimeOffset dto 
                    ? dto 
                    : DateTimeOffset.Parse(value.ToString() ?? string.Empty),
                PlaceholderDataType.String => value.ToString(),
                _ => value
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveDocumentAsync()
    {
        try
        {
            IsSaving = true;
            StatusMessage = "Проверка данных...";

            // Валидация
            var invalidFields = Fields.Where(f => !f.IsValid).ToList();
            if (invalidFields.Any())
            {
                StatusMessage = $"Исправьте ошибки в {invalidFields.Count} полях";
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFileName))
            {
                StatusMessage = "Укажите имя выходного файла";
                return;
            }

            if (!Directory.Exists(OutputDirectory))
            {
                StatusMessage = "Указанный каталог не существует";
                return;
            }

            StatusMessage = "Генерация документа...";

            // Подготовить значения для генерации
            var values = new Dictionary<string, PlaceholderValue>();
            foreach (var field in Fields)
            {
                values[field.Placeholder.Name] = new PlaceholderValue
                {
                    PlaceholderName = field.Placeholder.Name,
                    RawValue = field.Value,
                    FormattedPreviews = field.PreviewTexts
                };
            }

            // Сгенерировать путь вывода
            var outputPath = Path.Combine(OutputDirectory, OutputFileName + ".docx");

            // Проверить перезапись
            if (File.Exists(outputPath))
            {
                // TODO: Добавить диалог подтверждения
                StatusMessage = "Файл будет перезаписан...";
            }

            // Генерация документа
            var success = await _documentGenerationService.GenerateDocumentAsync(
                _template.FilePath,
                outputPath,
                values,
                _culture.Name);

            if (!success)
            {
                StatusMessage = "Ошибка при генерации документа";
                return;
            }

            StatusMessage = "Сохранение значений...";

            // Сохранить значения для будущего использования
            var fieldValues = Fields.ToDictionary(
                f => f.Placeholder.Name,
                f => f.Value ?? (object)string.Empty);

            await _valuePersistenceService.SaveTemplateValuesAsync(
                _template.FileName + ".docx",
                fieldValues);

            StatusMessage = $"Документ успешно сохранен: {outputPath}";

            // Открыть документ, если требуется
            if (OpenAfterSave)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = outputPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusMessage += $"\nОшибка при открытии: {ex.Message}";
                }
            }

            DocumentSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void BrowseOutputDirectory()
    {
        // TODO: Реализовать диалог выбора папки через Avalonia
        // Пока используем стандартное расположение
    }

    private void ClearFields()
    {
        foreach (var field in Fields)
        {
            field.Value = null;
        }
    }
}
