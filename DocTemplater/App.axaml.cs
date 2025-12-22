using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using DocTemplater.Views;

namespace DocTemplater;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Инициализация сервисов
            var templatesFolderPath = Path.Combine(AppContext.BaseDirectory, "Templates");
            var templateDiscoveryService = new TemplateDiscoveryService(templatesFolderPath);
            var placeholderExtractionService = new PlaceholderExtractionService();
            var documentGenerationService = new DocumentGenerationService();
            var valuePersistenceService = new ValuePersistenceService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    templateDiscoveryService,
                    placeholderExtractionService,
                    documentGenerationService,
                    valuePersistenceService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}