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
    
    private List<Template> _templates = new();
    private Template? _selectedTemplate;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public TemplateSelectionViewModel(
        ITemplateDiscoveryService templateDiscoveryService,
        IPlaceholderExtractionService placeholderExtractionService)
    {
        _templateDiscoveryService = templateDiscoveryService;
        _placeholderExtractionService = placeholderExtractionService;

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
}
