using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DocTemplater.Domain;
using DocTemplater.Services;
using ReactiveUI;

namespace DocTemplater.ViewModels;

/// <summary>
/// ViewModel для экрана ввода данных
/// </summary>
public class DataInputViewModel : ViewModelBase
{
    private const string ValuesSavedStatus = "Значения сохранены";
    private readonly Template _template;
    private readonly IDocumentGenerationService _documentGenerationService;
    private readonly IValuePersistenceService _valuePersistenceService;
    private readonly CultureInfo _culture;
    private readonly string _templateDirectory;
    private readonly CompositeDisposable _fieldSubscriptions = new();
    
    private List<PlaceholderFieldViewModel> _fields = new();
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _outputFileName = string.Empty;
    private string _outputFileNameTemplate = string.Empty;
    private int _outputFileNameTemplateCaretIndex = -1;
    private int _outputFileNameTemplateSelectionStart = -1;
    private int _outputFileNameTemplateSelectionEnd = -1;
    private bool _openAfterSave = false;
    private bool _isSaving;
    private bool _isLoading = true;
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
        
        // Сохранить директорию шаблона
        _templateDirectory = Path.GetDirectoryName(template.FilePath) ?? string.Empty;

        // Установить имя выходного файла по умолчанию
        OutputFileNameTemplate = $"{template.FileName}_заполненный";
        
        // Установить директорию вывода в ту же папку, где шаблон
        if (!string.IsNullOrEmpty(_templateDirectory))
        {
            OutputDirectory = _templateDirectory;
        }

        // Команды с явным указанием планировщика для UI-потока
        var canEdit = this.WhenAnyValue(x => x.IsBusy).Select(busy => !busy);
        SaveCommand = ReactiveCommand.CreateFromTask(
            SaveDocumentAsync, canEdit,
            outputScheduler: RxApp.MainThreadScheduler);
        SaveValuesCommand = ReactiveCommand.CreateFromTask(
            SaveValuesAsync, canEdit, outputScheduler: RxApp.MainThreadScheduler);
        BackCommand = ReactiveCommand.Create(() => { }, canEdit);
        BrowseOutputDirectoryCommand = ReactiveCommand.Create(BrowseOutputDirectory, canEdit);
        BrowseOutputPathCommand = ReactiveCommand.Create(BrowseOutputDirectory, canEdit);
        ClearCommand = ReactiveCommand.Create(ClearFields, canEdit);

        // Загрузить сохраненные значения и создать поля
        LoadSavedValuesCommand = ReactiveCommand.CreateFromTask(
            LoadSavedValuesAsync,
            outputScheduler: RxApp.MainThreadScheduler);
        LoadSavedValuesCommand.Execute().Subscribe();
    }

    /// <summary>
    /// Поля для ввода
    /// </summary>
    public List<PlaceholderFieldViewModel> Fields
    {
        get => _fields;
        set
        {
            foreach (var field in _fields)
            {
                field.SetInsertPlaceholderTokenHandler(null);
            }

            _fieldSubscriptions.Clear();
            this.RaiseAndSetIfChanged(ref _fields, value ?? new List<PlaceholderFieldViewModel>());

            foreach (var field in _fields)
            {
                field.SetInsertPlaceholderTokenHandler(InsertPlaceholderToken);
                _fieldSubscriptions.Add(field.WhenAnyValue(x => x.Value)
                    .Skip(1)
                    .Subscribe(_ =>
                    {
                        UpdateOutputFileName();
                        if (StatusMessage == ValuesSavedStatus)
                            StatusMessage = "Есть несохранённые изменения";
                    }));
            }

            UpdateOutputFileName();
        }
    }

    /// <summary>
    /// Каталог вывода
    /// </summary>
    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            var oldValue = _outputDirectory;
            this.RaiseAndSetIfChanged(ref _outputDirectory, value);
            if (oldValue != value)
            {
                // Обновить OutputFilePath при изменении директории
                this.RaisePropertyChanged(nameof(OutputFilePath));
            }
        }
    }

    /// <summary>
    /// Имя выходного файла
    /// </summary>
    public string OutputFileName
    {
        get => _outputFileName;
        set => OutputFileNameTemplate = value;
    }

    /// <summary>
    /// Шаблон имени выходного файла
    /// </summary>
    public string OutputFileNameTemplate
    {
        get => _outputFileNameTemplate;
        set
        {
            var newValue = value ?? string.Empty;
            var oldValue = _outputFileNameTemplate;
            this.RaiseAndSetIfChanged(ref _outputFileNameTemplate, newValue);
            if (oldValue != newValue)
            {
                UpdateOutputFileName();
            }
        }
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
        set
        {
            this.RaiseAndSetIfChanged(ref _isSaving, value);
            this.RaisePropertyChanged(nameof(IsBusy));
        }
    }

    public bool IsBusy => _isLoading || IsSaving;

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
            {
                OutputDirectory = directory;
            }
            if (!string.IsNullOrEmpty(fileName))
            {
                OutputFileNameTemplate = fileName;
            }
            // Явно уведомить об изменении
            this.RaisePropertyChanged(nameof(OutputFilePath));
        }
    }

    /// <summary>
    /// Команда сохранения
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveValuesCommand { get; }

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
    /// Функция для вызова диалога сохранения файла (устанавливается из View)
    /// </summary>
    public Func<string, Task<string?>>? BrowseForSaveFileAsync { get; set; }

    /// <summary>
    /// Функция для подтверждения перезаписи существующего файла
    /// </summary>
    public Func<string, Task<bool>>? ConfirmFileOverwriteAsync { get; set; }

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

    public event Action<int>? OutputFileNameTemplateFocusRequested;

    public void UpdateOutputFileNameTemplateSelection(int caretIndex, int selectionStart, int selectionEnd)
    {
        _outputFileNameTemplateCaretIndex = caretIndex;
        _outputFileNameTemplateSelectionStart = selectionStart;
        _outputFileNameTemplateSelectionEnd = selectionEnd;
    }

    public void InsertPlaceholderToken(PlaceholderFieldViewModel field)
    {
        InsertPlaceholderToken(field.PlaceholderToken);
    }

    public void InsertPlaceholderToken(string token)
    {
        var template = OutputFileNameTemplate;
        var length = template.Length;
        var hasSelection = _outputFileNameTemplateSelectionStart >= 0 &&
                           _outputFileNameTemplateSelectionEnd >= 0 &&
                           _outputFileNameTemplateSelectionStart != _outputFileNameTemplateSelectionEnd;

        var start = hasSelection
            ? Math.Clamp(Math.Min(_outputFileNameTemplateSelectionStart, _outputFileNameTemplateSelectionEnd), 0, length)
            : Math.Clamp(_outputFileNameTemplateCaretIndex < 0 ? length : _outputFileNameTemplateCaretIndex, 0, length);
        var end = hasSelection
            ? Math.Clamp(Math.Max(_outputFileNameTemplateSelectionStart, _outputFileNameTemplateSelectionEnd), 0, length)
            : start;

        OutputFileNameTemplate = template.Remove(start, end - start).Insert(start, token);

        var caretIndex = start + token.Length;
        UpdateOutputFileNameTemplateSelection(caretIndex, caretIndex, caretIndex);
        OutputFileNameTemplateFocusRequested?.Invoke(caretIndex);
    }

    private async Task LoadSavedValuesAsync()
    {
        _isLoading = true;
        this.RaisePropertyChanged(nameof(IsBusy));
        try
        {
            // Получить сохраненные значения
            var savedData = await _valuePersistenceService.GetSavedValuesAsync(_template.FileName + ".docx");

            // Загрузить последний каталог сохранения, если он существует
            if (savedData?.LastSaveDirectory != null && Directory.Exists(savedData.LastSaveDirectory))
            {
                OutputDirectory = savedData.LastSaveDirectory;
            }

            if (!string.IsNullOrWhiteSpace(savedData?.OutputFileNameTemplate))
            {
                OutputFileNameTemplate = savedData.OutputFileNameTemplate;
            }

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
        finally
        {
            _isLoading = false;
            this.RaisePropertyChanged(nameof(IsBusy));
        }
    }

    private object? ConvertSavedValue(object value, PlaceholderDataType dataType)
    {
        try
        {
            // Обработка JsonElement из System.Text.Json
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return dataType switch
                {
                    PlaceholderDataType.Integer => jsonElement.GetInt32(),
                    PlaceholderDataType.DateTimeOffset => jsonElement.GetDateTimeOffset(),
                    PlaceholderDataType.String => jsonElement.GetString(),
                    _ => jsonElement.ToString()
                };
            }
            
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

    private void UpdateOutputFileName()
    {
        var outputFileName = RenderOutputFileNameTemplate();
        if (_outputFileName != outputFileName)
        {
            this.RaiseAndSetIfChanged(ref _outputFileName, outputFileName, nameof(OutputFileName));
            this.RaisePropertyChanged(nameof(OutputFilePath));
        }
    }

    private string RenderOutputFileNameTemplate()
    {
        var outputFileName = OutputFileNameTemplate;

        foreach (var field in Fields)
        {
            outputFileName = outputFileName.Replace(
                field.PlaceholderToken,
                FormatFieldValueForFileName(field),
                StringComparison.Ordinal);
        }

        outputFileName = SanitizeFileName(outputFileName).Trim();
        return RemoveTrailingDocxExtension(outputFileName).Trim();
    }

    private string FormatFieldValueForFileName(PlaceholderFieldViewModel field)
    {
        if (field.Value == null || !field.IsValid)
        {
            return string.Empty;
        }

        var format = field.Placeholder.FormatPatterns
            .Where(pattern => !string.IsNullOrEmpty(pattern))
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .FirstOrDefault();

        try
        {
            return field.Value switch
            {
                DateTimeOffset dto => string.IsNullOrEmpty(format)
                    ? dto.ToString(_culture)
                    : dto.ToString(format, _culture),
                DateTime dt => string.IsNullOrEmpty(format)
                    ? dt.ToString(_culture)
                    : dt.ToString(format, _culture),
                int i => string.IsNullOrEmpty(format)
                    ? i.ToString(_culture)
                    : i.ToString(format, _culture),
                IFormattable formattable => formattable.ToString(format, _culture),
                string s => s,
                _ => field.Value.ToString() ?? string.Empty
            };
        }
        catch
        {
            return field.Value.ToString() ?? string.Empty;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character);
        return new string(sanitizedCharacters.ToArray());
    }

    private static string RemoveTrailingDocxExtension(string fileName)
    {
        const string extension = ".docx";
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^extension.Length]
            : fileName;
    }

    private async Task SaveDocumentAsync()
    {
        if (IsBusy)
            return;
        try
        {
            IsSaving = true;
            StatusMessage = "Проверка данных...";

            // Валидация
            if (!ValidateFields())
                return;

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
                StatusMessage = "Ожидание подтверждения перезаписи...";
                if (ConfirmFileOverwriteAsync == null ||
                    !await ConfirmFileOverwriteAsync(outputPath))
                {
                    StatusMessage = "Сохранение отменено";
                    return;
                }

                StatusMessage = "Генерация документа...";
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
                fieldValues,
                OutputDirectory,
                OutputFileNameTemplate);

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
        // Вызов асинхронного диалога через RxApp.MainThreadScheduler
        Observable.FromAsync(async () =>
        {
            if (BrowseForSaveFileAsync != null)
            {
                // Передать директорию шаблонов как начальную директорию
                var result = await BrowseForSaveFileAsync(_templateDirectory);
                if (!string.IsNullOrEmpty(result))
                {
                    OutputFilePath = result;
                }
            }
        }).Subscribe();
    }

    private bool ValidateFields()
    {
        foreach (var field in Fields)
            field.Validate();
        var invalidCount = Fields.Count(field => !field.IsValid);
        if (invalidCount == 0)
            return true;
        StatusMessage = $"Исправьте ошибки в {invalidCount} полях";
        return false;
    }

    private async Task SaveValuesAsync()
    {
        if (IsBusy)
            return;
        try
        {
            IsSaving = true;
            StatusMessage = "Проверка данных...";
            if (!ValidateFields())
                return;

            var values = Fields.ToDictionary(field => field.Placeholder.Name, field => field.Value!);
            StatusMessage = "Сохранение значений...";
            var templateKey = _template.FileName + ".docx";
            var previous = await _valuePersistenceService.GetSavedValuesAsync(templateKey);
            var mergedValues = previous == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(previous.FieldValues);
            foreach (var value in values)
                mergedValues[value.Key] = value.Value;

            await _valuePersistenceService.SaveTemplateValuesAsync(templateKey, mergedValues);
            StatusMessage = ValuesSavedStatus;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения значений: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ClearFields()
    {
        foreach (var field in Fields)
        {
            field.Value = null;
        }
    }
}
