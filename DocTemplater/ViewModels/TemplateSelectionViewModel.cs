using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using DocTemplater.Domain;
using DocTemplater.Services;
using ReactiveUI;

namespace DocTemplater.ViewModels;

/// <summary>
/// ViewModel для экрана выбора шаблона
/// </summary>
public class TemplateSelectionViewModel : ViewModelBase
{
    private readonly ITemplateDiscoveryService _templateDiscoveryService;
    private readonly IPlaceholderExtractionService _placeholderExtractionService;
    private readonly IValuePersistenceService? _valuePersistenceService;
    
    private List<Template> _templates = new();
    private Template? _selectedTemplate;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public TemplateSelectionViewModel(
        ITemplateDiscoveryService templateDiscoveryService,
        IPlaceholderExtractionService placeholderExtractionService,
        IValuePersistenceService? valuePersistenceService = null)
    {
        _templateDiscoveryService = templateDiscoveryService;
        _placeholderExtractionService = placeholderExtractionService;
        _valuePersistenceService = valuePersistenceService;

        // Подписаться на изменения в директории шаблонов
        if (_templateDiscoveryService is TemplateDiscoveryService tds)
        {
            tds.TemplatesChanged += OnTemplatesChanged;
        }

        // Команды с явным указанием планировщика для UI-потока
        LoadTemplatesCommand = ReactiveCommand.CreateFromTask(
            LoadTemplatesAsync,
            outputScheduler: RxApp.MainThreadScheduler);
        SelectTemplateCommand = ReactiveCommand.CreateFromTask(
            SelectTemplateAsync,
            this.WhenAnyValue(x => x.SelectedTemplate, (Template? template) => template != null),
            RxApp.MainThreadScheduler);
        RefreshCommand = ReactiveCommand.CreateFromTask(
            RefreshTemplatesAsync,
            outputScheduler: RxApp.MainThreadScheduler);
        EditTemplateCommand = ReactiveCommand.Create(
            EditTemplate,
            this.WhenAnyValue(x => x.SelectedTemplate, (Template? template) => template != null));
        ChangeTemplatesDirectoryCommand = ReactiveCommand.Create(() => { });

        // Автоматическая загрузка при создании
        LoadTemplatesCommand.Execute().Subscribe();
    }

    /// <summary>
    /// Список доступных шаблонов
    /// </summary>
    public List<Template> Templates
    {
        get => _templates;
        set => this.RaiseAndSetIfChanged(ref _templates, value);
    }

    /// <summary>
    /// Выбранный шаблон
    /// </summary>
    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set => this.RaiseAndSetIfChanged(ref _selectedTemplate, value);
    }

    /// <summary>
    /// Флаг загрузки
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
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
    /// Команда загрузки шаблонов
    /// </summary>
    public ReactiveCommand<Unit, Unit> LoadTemplatesCommand { get; }

    /// <summary>
    /// Команда выбора шаблона
    /// </summary>
    public ReactiveCommand<Unit, Unit> SelectTemplateCommand { get; }

    /// <summary>
    /// Команда обновления списка
    /// </summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>
    /// Команда редактирования шаблона
    /// </summary>
    public ReactiveCommand<Unit, Unit> EditTemplateCommand { get; }

    /// <summary>
    /// Команда изменения директории шаблонов
    /// </summary>
    public ReactiveCommand<Unit, Unit> ChangeTemplatesDirectoryCommand { get; }

    /// <summary>
    /// Функция для выбора директории (устанавливается из View)
    /// </summary>
    public Func<Task<string?>>? BrowseForFolderAsync { get; set; }

    /// <summary>
    /// Событие навигации к форме ввода данных
    /// </summary>
    public event EventHandler<Template>? NavigateToDataInput;

    private async Task LoadTemplatesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Загрузка шаблонов...";

            Templates = await _templateDiscoveryService.DiscoverTemplatesAsync();
            
            StatusMessage = Templates.Count > 0 
                ? $"Найдено {Templates.Count} шаблонов" 
                : "Шаблоны не найдены. Добавьте .docx файлы в папку Templates.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при загрузке шаблонов: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshTemplatesAsync()
    {
        SelectedTemplate = null;
        await LoadTemplatesAsync();
    }

    private async Task SelectTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "Извлечение плейсхолдеров...";

            // Извлечь плейсхолдеры из выбранного шаблона
            var placeholders = await _placeholderExtractionService.ExtractPlaceholdersAsync(SelectedTemplate.FilePath);
            SelectedTemplate.Placeholders = placeholders;

            StatusMessage = $"Извлечено {placeholders.Count} плейсхолдеров";

            // Вызвать событие навигации
            NavigateToDataInput?.Invoke(this, SelectedTemplate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при обработке шаблона: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void EditTemplate()
    {
        if (SelectedTemplate == null)
            return;

        try
        {
            // Открыть шаблон с помощью системного приложения по умолчанию
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedTemplate.FilePath,
                UseShellExecute = true
            });
            StatusMessage = $"Открыт шаблон: {SelectedTemplate.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при открытии шаблона: {ex.Message}";
        }
    }

    /// <summary>
    /// Обработчик изменений в директории шаблонов
    /// </summary>
    private void OnTemplatesChanged(object? sender, EventArgs e)
    {
        // Обновить список шаблонов в UI-потоке
        RxApp.MainThreadScheduler.Schedule(async () =>
        {
            // Добавить небольшую задержку, чтобы избежать множественных обновлений
            await Task.Delay(500);
            await LoadTemplatesAsync();
        });
    }

    /// <summary>
    /// Обработчик команды изменения директории
    /// </summary>
    public async Task ChangeTemplatesDirectoryAsync()
    {
        if (BrowseForFolderAsync == null)
            return;

        var selectedPath = await BrowseForFolderAsync();
        if (string.IsNullOrEmpty(selectedPath))
            return;

        try
        {
            // Установить новую директорию
            _templateDiscoveryService.SetTemplatesDirectory(selectedPath);
            
            // Сохранить в настройки
            if (_valuePersistenceService != null)
            {
                var settings = await _valuePersistenceService.LoadSettingsAsync();
                settings.TemplatesFolderPath = selectedPath;
                await _valuePersistenceService.SaveSettingsAsync(settings);
            }
            
            StatusMessage = $"Директория шаблонов изменена: {selectedPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка при изменении директории: {ex.Message}";
        }
    }
}
