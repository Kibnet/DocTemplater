using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Velopack.Locators;
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

            var window = new MainWindow();
            desktop.MainWindow = window;
            var originalContent = window.Content;
            var locator = VelopackLocator.Current;
            var roots = new[] { locator.RootAppDir, locator.AppContentDir, locator.PackagesDir, locator.AppTempDir }
                .Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().ToList();
            roots.Add(AppContext.BaseDirectory);
            var safety = new ApplicationDataSafety(Program.DataRoot, roots, AppContext.BaseDirectory);
            var persistence = new ValuePersistenceService(safety.SettingsPath);
            async Task InitializeWorkingWindowAsync(bool importAdjacentTemplates = true)
            {
                try
                {
                    var settings = await safety.BootstrapAsync(persistence, importAdjacentTemplates);
                    var discovery = new TemplateDiscoveryService(settings.TemplatesFolderPath);
                    var updates = new ApplicationUpdateService(new VelopackUpdateAdapter(), action =>
                    {
                        if (Dispatcher.UIThread.CheckAccess()) action(); else Dispatcher.UIThread.Invoke(action);
                    });
                    window.Content = originalContent;
                    window.DataContext = new MainWindowViewModel(discovery, new PlaceholderExtractionService(), new DocumentGenerationService(), persistence, updates, safety);
                    desktop.Exit += (_, _) => { updates.Dispose(); discovery.Dispose(); };
                    await updates.CheckAndDownloadAsync();
                }
                catch (Exception ex)
                {
                    ShowStartupError(ex);
                }
            }
            void ShowStartupError(Exception error)
            {
                var message = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Text = "Запуск остановлен для защиты данных.\n\n" + error.Message + "\n\nМожно выбрать внешний каталог шаблонов. Для повреждённых настроек сначала восстановите settings.json из резервной копии." };
                var choose = new Button { Content = "Выбрать внешний каталог шаблонов…" };
                choose.Click += async (_, _) =>
                {
                    choose.IsEnabled = false;
                    try
                    {
                        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Внешний каталог шаблонов", AllowMultiple = false });
                        var path = folders.FirstOrDefault()?.TryGetLocalPath();
                        if (path == null) return;
                        ApplicationInstanceCoordinator.EnsureNoOtherProcess();
                        safety.EnsureSafeUserDirectory(path);
                        await safety.PreserveCurrentUserDirectoriesAsync(persistence); // strict read + durable recovery context before path patch
                        File.Copy(safety.SettingsPath, safety.SettingsPath + ".before-folder-choice-" + Guid.NewGuid().ToString("N") + ".bak");
                        await persistence.SaveTemplatesDirectoryAsync(path);
                        await InitializeWorkingWindowAsync(importAdjacentTemplates: false);
                    }
                    catch (Exception ex) { message.Text = "Каталог не изменён: " + ex.Message; }
                    finally { choose.IsEnabled = true; }
                };
                window.Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children = { message, choose } };
            }
            window.Opened += async (_, _) => await InitializeWorkingWindowAsync();
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
