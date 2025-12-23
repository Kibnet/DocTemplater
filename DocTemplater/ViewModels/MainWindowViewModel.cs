using DocTemplater.Domain;
using DocTemplater.Services;
using ReactiveUI;

namespace DocTemplater.ViewModels;

/// <summary>
/// ViewModel главного окна
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITemplateDiscoveryService _templateDiscoveryService;
    private readonly IPlaceholderExtractionService _placeholderExtractionService;
    private readonly IDocumentGenerationService _documentGenerationService;
    private readonly IValuePersistenceService _valuePersistenceService;
    
    private ViewModelBase _currentView;

    public MainWindowViewModel(
        ITemplateDiscoveryService templateDiscoveryService,
        IPlaceholderExtractionService placeholderExtractionService,
        IDocumentGenerationService documentGenerationService,
        IValuePersistenceService valuePersistenceService)
    {
        _templateDiscoveryService = templateDiscoveryService;
        _placeholderExtractionService = placeholderExtractionService;
        _documentGenerationService = documentGenerationService;
        _valuePersistenceService = valuePersistenceService;

        // Начать с экрана выбора шаблона
        var templateSelectionVM = new TemplateSelectionViewModel(
            _templateDiscoveryService,
            _placeholderExtractionService,
            _valuePersistenceService);
        
        templateSelectionVM.NavigateToDataInput += OnNavigateToDataInput;
        
        _currentView = templateSelectionVM;
    }

    /// <summary>
    /// Текущее представление
    /// </summary>
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    private void OnNavigateToDataInput(object? sender, Template template)
    {
        var dataInputVM = new DataInputViewModel(
            template,
            _documentGenerationService,
            _valuePersistenceService);

        dataInputVM.BackCommand.Subscribe(_ => NavigateToTemplateSelection());
        dataInputVM.DocumentSaved += (_, _) => NavigateToTemplateSelection();

        CurrentView = dataInputVM;
    }

    private void NavigateToTemplateSelection()
    {
        var templateSelectionVM = new TemplateSelectionViewModel(
            _templateDiscoveryService,
            _placeholderExtractionService,
            _valuePersistenceService);
        
        templateSelectionVM.NavigateToDataInput += OnNavigateToDataInput;
        
        CurrentView = templateSelectionVM;
    }
}
