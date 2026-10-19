using DocTemplater.Domain;
using DocTemplater.Services;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

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
    
    private ViewModelBase _currentView = null!;

    public MainWindowViewModel(
        ITemplateDiscoveryService templateDiscoveryService,
        IPlaceholderExtractionService placeholderExtractionService,
        IDocumentGenerationService documentGenerationService,
        IValuePersistenceService valuePersistenceService,
        ApplicationUpdateService? updates = null,
        ApplicationDataSafety? dataSafety = null)
    {
        _templateDiscoveryService = templateDiscoveryService;
        _placeholderExtractionService = placeholderExtractionService;
        _documentGenerationService = documentGenerationService;
        _valuePersistenceService = valuePersistenceService;
        Updates = updates ?? new ApplicationUpdateService(new DisabledUpdateAdapter());
        _dataSafety = dataSafety;
        CheckUpdatesCommand = ReactiveCommand.CreateFromTask(Updates.CheckAndDownloadAsync,
            Updates.WhenAnyValue(x => x.CanCheck));
        var documentBusy = this.WhenAnyValue(x => x.CurrentView).Select(view => view switch
        {
            DataInputViewModel data => data.WhenAnyValue(x => x.IsBusy),
            TemplateSelectionViewModel selection => selection.WhenAnyValue(x => x.IsLoading, x => x.IsChangingDirectory, (loading, changing) => loading || changing),
            _ => Observable.Return(false)
        }).Switch();
        ApplyUpdateCommand = ReactiveCommand.CreateFromTask(ApplyUpdateAsync,
            Updates.WhenAnyValue(x => x.IsReady).CombineLatest(documentBusy, (ready, busy) => ready && !busy));

        // Начать с экрана выбора шаблона
        var templateSelectionVM = new TemplateSelectionViewModel(
            _templateDiscoveryService,
            _placeholderExtractionService,
            _valuePersistenceService);
        
        templateSelectionVM.NavigateToDataInput += OnNavigateToDataInput;
        
        CurrentView = templateSelectionVM;
    }

    private readonly ApplicationDataSafety? _dataSafety;
    public ApplicationUpdateService Updates { get; }
    public ReactiveCommand<Unit, Unit> CheckUpdatesCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyUpdateCommand { get; }
    public Func<Task<bool>>? ConfirmDiscardForUpdateAsync { get; set; }
    private bool _applying;
    private bool DocumentBusy => CurrentView is DataInputViewModel data && data.IsBusy
        || CurrentView is TemplateSelectionViewModel selection && (selection.IsLoading || selection.IsChangingDirectory);

    public async Task ApplyUpdateAsync()
    {
        if (_applying || !Updates.IsReady) return;
        _applying = true;
        try
        {
            if (DocumentBusy) { Updates.ReportBlocked("Дождитесь окончания работы с документом или каталогом."); return; }
            if (CurrentView is DataInputViewModel { HasUnsavedChanges: true }
                && (ConfirmDiscardForUpdateAsync == null || !await ConfirmDiscardForUpdateAsync())) return;
            // Recheck after any await: a picker/save may have started while confirmation was open.
            if (DocumentBusy) { Updates.ReportBlocked("Дождитесь окончания работы с документом или каталогом."); return; }
            ApplicationInstanceCoordinator.EnsureNoOtherProcess();
            var settings = await _valuePersistenceService.LoadSettingsAsync();
            _dataSafety?.EnsureSafeForUpdate(settings, (CurrentView as DataInputViewModel)?.OutputDirectory);
            if (DocumentBusy) { Updates.ReportBlocked("Дождитесь окончания работы с документом или каталогом."); return; }
            Updates.ApplyAndRestart();
        }
        catch (Exception ex) { Updates.ReportBlocked("Обновление остановлено: " + ex.Message); }
        finally { _applying = false; }
    }

    private sealed class DisabledUpdateAdapter : IApplicationUpdateAdapter
    {
        public bool IsInstalled => false;
        public string Version => typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        public bool HasPendingUpdate => false;
        public Task<bool> CheckAsync() => Task.FromResult(false);
        public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public void ApplyAndRestart() { }
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
            _valuePersistenceService,
            ensureSafeOutput: _dataSafety == null ? null : _dataSafety.EnsureSafeUserDirectory);

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
