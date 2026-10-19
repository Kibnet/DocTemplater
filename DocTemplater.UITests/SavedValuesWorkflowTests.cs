using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using DocTemplater.Views;

namespace DocTemplater.UITests;

public class SavedValuesWorkflowTests
{
    [AvaloniaFact]
    public async Task SaveValues_InProgress_DisablesEditorsNavigationAndBothSaveButtons()
    {
        var persistence = new GatedPersistence();
        var vm = new DataInputViewModel(new Template
        {
            FileName = "Договор", FilePath = "Договор.docx",
            Placeholders = new() { new() { Name = "Заказчик", DataType = PlaceholderDataType.String } }
        }, new CountingGenerator(), persistence);
        await WaitForLoad(vm);
        vm.Fields[0].ValueText = "ООО Пример";
        var view = new DataInputView { DataContext = vm };
        var window = Show(view, 800);
        try
        {
            await Click(Find<Button>(view, "SaveValuesButton"));
            Assert.True(vm.IsBusy);
            Assert.False(Find<Button>(view, "BackButton").IsEffectivelyEnabled);
            Assert.False(Find<Button>(view, "ClearButton").IsEffectivelyEnabled);
            Assert.False(Find<Button>(view, "SaveButton").IsEffectivelyEnabled);
            Assert.False(Find<Button>(view, "SaveValuesButton").IsEffectivelyEnabled);
            Assert.False(Find<TextBox>(view, "OutputFileNameTemplateTextBox").IsEffectivelyEnabled);
            Assert.All(view.GetVisualDescendants().OfType<TextBox>().Where(box => box.Name == "ValueTextBox"),
                box => Assert.False(box.IsEffectivelyEnabled));
            Capture(window, "values-saving-800");
        }
        finally
        {
            persistence.Completion.TrySetResult();
            await WaitForLoad(vm);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SaveValuesButton_PersistsEdits_ShowsFeedbackAndKeepsDocumentActionSeparate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DocTemplater-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Window? window = null;
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var persistence = new ValuePersistenceService(path);
            await persistence.SaveSettingsAsync(new AppSettings());
            var generator = new CountingGenerator();
            var template = new Template
            {
                FileName = "Договор", FilePath = Path.Combine(directory, "Договор.docx"),
                Placeholders = new()
                {
                    new() { Name = "Заказчик", DataType = PlaceholderDataType.String },
                    new() { Name = "Количество", DataType = PlaceholderDataType.Integer }
                }
            };
            var vm = new DataInputViewModel(template, generator, persistence);
            await WaitForLoad(vm);
            vm.OutputDirectory = directory;
            var view = new DataInputView { DataContext = vm };
            window = Show(view, 800);
            var saveValues = Find<Button>(view, "SaveValuesButton");
            var saveDocument = Find<Button>(view, "SaveButton");
            var navigated = false;
            vm.DocumentSaved += (_, _) => navigated = true;

            await Click(saveValues);
            Assert.All(vm.Fields, field => Assert.False(field.IsValid));
            Assert.Contains("Исправьте ошибки", Find<TextBlock>(view, "StatusMessageTextBlock").Text);
            Capture(window, "values-validation-error-800");

            var textBox = view.GetVisualDescendants().OfType<TextBox>()
                .Single(box => box.Name == "ValueTextBox" && ReferenceEquals(box.DataContext, vm.Fields[0]));
            textBox.Text = "ООО Пример";
            var number = view.GetVisualDescendants().OfType<NumericUpDown>()
                .Single(editor => editor.Name == "IntegerValueUpDown" && ReferenceEquals(editor.DataContext, vm.Fields[1]));
            number.Value = 12;
            Dispatcher.UIThread.RunJobs();

            using (var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await Click(saveValues);
                await WaitForLoad(vm);
                Assert.StartsWith("Ошибка сохранения значений:", vm.StatusMessage);
                Assert.Equal("ООО Пример", vm.Fields[0].ValueText);
                Capture(window, "values-write-error-800");
            }

            await Click(saveValues);
            await WaitForLoad(vm);
            Assert.Equal("Значения сохранены", Find<TextBlock>(view, "StatusMessageTextBlock").Text);
            Assert.False(navigated);
            Assert.Equal(0, generator.Calls);
            Assert.Empty(Directory.GetFiles(directory, "*.docx"));
            var fresh = new ValuePersistenceService(path);
            var reopened = new DataInputViewModel(template, generator, fresh);
            await WaitForLoad(reopened);
            Assert.Equal("ООО Пример", reopened.Fields[0].ValueText);
            Assert.Equal(12, reopened.Fields[1].IntegerValue);

            foreach (var width in new[] { 800d, 900d })
            {
                window.Width = width;
                window.UpdateLayout();
                foreach (var name in new[] { "SaveValuesButton", "SaveButton", "BackButton", "BrowseOutputPathButton", "OutputFilePathTextBox", "StatusMessageTextBlock" })
                    AssertInsideWindow(window, Find<Control>(view, name));
                Capture(window, "values-saved-" + width);
            }

            textBox.Focus();
            textBox.SelectAll();
            window.KeyTextInput("ООО После правки");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("ООО После правки", vm.Fields[0].ValueText);
            Assert.Equal("Есть несохранённые изменения", Find<TextBlock>(view, "StatusMessageTextBlock").Text);
            var beforeResave = new ValuePersistenceService(path);
            Assert.Equal("ООО Пример", (await beforeResave.GetSavedValuesAsync("Договор.docx"))!.FieldValues["Заказчик"].ToString());
            Capture(window, "values-unsaved-900");

            await Click(saveValues);
            await WaitForLoad(vm);
            Assert.Equal("Значения сохранены", Find<TextBlock>(view, "StatusMessageTextBlock").Text);
            var afterResave = new ValuePersistenceService(path);
            Assert.Equal("ООО После правки", (await afterResave.GetSavedValuesAsync("Договор.docx"))!.FieldValues["Заказчик"].ToString());

            await Click(saveDocument);
            await WaitForLoad(vm);
            Assert.Equal(1, generator.Calls);
            Assert.True(navigated);
        }
        finally
        {
            window?.Close();
            Directory.Delete(directory, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenFolderButton_UsesActiveDirectoryWithEmptySelection_AndDisplaysShellError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DocTemplater-ui-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Window? window = null;
        try
        {
            var opener = new RecordingOpener();
            var discovery = new EmptyDiscovery(directory);
            var vm = new TemplateSelectionViewModel(discovery, new PlaceholderExtractionService(), directoryOpener: opener);
            var view = new TemplateSelectionView { DataContext = vm };
            window = Show(view, 800);
            Assert.Null(vm.SelectedTemplate);
            Assert.Empty(vm.Templates);
            var button = Find<Button>(view, "OpenTemplatesDirectoryButton");
            await Click(button);
            Assert.Equal(directory, opener.LastPath);
            Assert.Contains("Отправлен запрос", vm.StatusMessage);
            foreach (var width in new[] { 800d, 900d })
            {
                window.Width = width;
                window.UpdateLayout();
                AssertInsideWindow(window, button);
                AssertInsideWindow(window, Find<Button>(view, "SelectTemplateButton"));
                Capture(window, "templates-folder-" + width);
            }
            var other = Path.Combine(directory, "Другие шаблоны");
            Directory.CreateDirectory(other);
            var pathBox = view.GetVisualDescendants().OfType<TextBox>().Single();
            pathBox.Focus();
            pathBox.SelectAll();
            window.KeyTextInput(other);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(other, vm.ActiveTemplatesDirectory);
            await Click(button);
            Assert.Equal(other, opener.LastPath);
            opener.Error = true;
            await Click(button);
            Assert.Contains("Ошибка при открытии папки", vm.StatusMessage);
            Capture(window, "templates-folder-error-900");
        }
        finally
        {
            window?.Close();
            Directory.Delete(directory, true);
        }
    }

    private static Window Show(Control view, double width)
    {
        var window = new Window { Content = view, Width = width, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task Click(Button button)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!button.IsEffectivelyEnabled && DateTime.UtcNow < deadline) await Task.Delay(5);
        Assert.True(button.IsEffectivelyEnabled,
            $"{button.Name}: enabled={button.IsEnabled}, command={button.Command?.CanExecute(null)}, busy={(button.DataContext as DataInputViewModel)?.IsBusy}; " +
            string.Join("; ", button.GetVisualAncestors().OfType<Control>().Select(c => $"{c.GetType().Name}/{c.Name}: {c.IsEnabled}/{c.IsEffectivelyEnabled}")));
        var window = (Window)TopLevel.GetTopLevel(button)!;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void AssertInsideWindow(Window window, Control control)
    {
        Assert.True(control.IsEffectivelyVisible);
        var position = control.TranslatePoint(default, window)!.Value;
        Assert.True(position.X >= 0 && position.Y >= 0 &&
                    position.X + control.Bounds.Width <= window.ClientSize.Width + 1 &&
                    position.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.Name}: {position}, {control.Bounds}, window {window.ClientSize}");
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("DOCTEMPLATER_UI_ARTIFACTS");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"));
    }

    private static async Task WaitForLoad(DataInputViewModel vm)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((vm.IsBusy || !((ICommand)vm.SaveValuesCommand).CanExecute(null)) && DateTime.UtcNow < deadline)
            await Task.Delay(5);
        Assert.False(vm.IsBusy);
        Assert.True(((ICommand)vm.SaveValuesCommand).CanExecute(null));
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class CountingGenerator : IDocumentGenerationService
    {
        public int Calls { get; private set; }
        public Task<bool> GenerateDocumentAsync(string templatePath, string outputPath,
            Dictionary<string, PlaceholderValue> values, string cultureName)
        { Calls++; return Task.FromResult(true); }
    }

    private sealed class GatedPersistence : IValuePersistenceService
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings());
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public Task SaveTemplatesDirectoryAsync(string directory) => Task.CompletedTask;
        public Task<SavedTemplateData?> GetSavedValuesAsync(string name) => Task.FromResult<SavedTemplateData?>(null);
        public Task SaveTemplateValuesAsync(string name, Dictionary<string, object> values,
            string? lastSaveDirectory = null, string? outputFileNameTemplate = null) => Completion.Task;
    }

    private sealed class RecordingOpener : IDirectoryOpener
    {
        public string? LastPath { get; private set; }
        public bool Error { get; set; }
        public void Open(string directory)
        {
            if (Error) throw new InvalidOperationException("Файловый менеджер недоступен");
            LastPath = directory;
        }
    }

    private sealed class EmptyDiscovery : ITemplateDiscoveryService
    {
        public EmptyDiscovery(string path) { TemplatesDirectory = path; }
        public string TemplatesDirectory { get; private set; }
        public Task<List<Template>> DiscoverTemplatesAsync() => Task.FromResult(new List<Template>());
        public Task<List<Template>> RefreshTemplatesAsync() => DiscoverTemplatesAsync();
        public void SetTemplatesDirectory(string path) { TemplatesDirectory = Path.GetFullPath(path); }
    }
}
