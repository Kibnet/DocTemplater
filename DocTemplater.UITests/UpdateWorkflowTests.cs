using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using DocTemplater.Views;

namespace DocTemplater.UITests;

public class UpdateWorkflowTests
{
    [AvaloniaFact]
    public async Task DownloadProgressAndNetworkErrorKeepDocumentStatusAndAllowRetry()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DocTemplater-update-error-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var persistence = new ValuePersistenceService(Path.Combine(directory, "settings.json"));
        await persistence.SaveSettingsAsync(new AppSettings { TemplatesFolderPath = directory });
        using var discovery = new TemplateDiscoveryService(directory);
        var adapter = new DownloadAdapter();
        using var updates = new ApplicationUpdateService(adapter);
        var vm = new MainWindowViewModel(discovery, new PlaceholderExtractionService(), new DocumentGenerationService(), persistence, updates);
        var window = new MainWindow { DataContext = vm, Width = 800, Height = 600 };
        window.Show();
        try
        {
            var selection = Assert.IsType<TemplateSelectionViewModel>(vm.CurrentView);
            await WaitFor(() => !selection.IsLoading && !selection.IsChangingDirectory);
            selection.StatusMessage = "Шаблоны доступны";
            var operation = updates.CheckAndDownloadAsync();
            Assert.Equal(ApplicationUpdateState.Downloading, updates.State);
            Assert.Contains("57%", updates.Status);
            Capture(window, "update-download-800");
            adapter.Download.SetException(new IOException("GitHub недоступен"));
            await operation;
            Assert.Equal(ApplicationUpdateState.Error, updates.State);
            Assert.Equal("Шаблоны доступны", selection.StatusMessage);
            Assert.True(updates.CanCheck);
            Assert.Same(selection, vm.CurrentView);
            Capture(window, "update-network-error-800");
        }
        finally { window.Close(); Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task DirtyFormCancelPreservesInputsAndReadyPackageAt800And900()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DocTemplater-update-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var persistence = new ValuePersistenceService(Path.Combine(directory, "settings.json"));
        await persistence.SaveSettingsAsync(new AppSettings { TemplatesFolderPath = directory });
        using var discovery = new TemplateDiscoveryService(directory);
        var adapter = new ReadyAdapter();
        using var updates = new ApplicationUpdateService(adapter);
        var vm = new MainWindowViewModel(discovery, new PlaceholderExtractionService(), new DocumentGenerationService(), persistence, updates);
        var data = new DataInputViewModel(new Template { FileName = "Договор", FilePath = Path.Combine(directory, "Договор.docx"),
            Placeholders = new() { new() { Name = "Заказчик", DataType = PlaceholderDataType.String } } }, new DocumentGenerationService(), persistence);
        while (data.IsBusy) await Task.Delay(5);
        Assert.False(data.HasUnsavedChanges);
        data.Fields[0].ValueText = "Несохранённое значение";
        Assert.True(data.HasUnsavedChanges);
        vm.CurrentView = data;
        var window = new MainWindow { DataContext = vm, Width = 800, Height = 650 };
        window.Show();
        try
        {
            foreach (var width in new[] { 800d, 900d })
            {
                window.Width = width;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Capture(window, "update-ready-" + width);
                var pending = vm.ApplyUpdateAsync();
                Dispatcher.UIThread.RunJobs();
                var dialog = window.OwnedWindows.Single();
                Assert.Equal("Несохранённые изменения", dialog.Title);
                dialog.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Capture(dialog, "update-dirty-" + width);
                var cancel = dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CancelUpdateButton");
                Assert.True(cancel.IsDefault);
                Click(dialog, cancel);
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(updates.IsReady);
                Assert.Equal(0, adapter.Applies);
                Assert.Equal("Несохранённое значение", data.Fields[0].ValueText);
            }
            var saveValues = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SaveValuesButton");
            Click(window, saveValues);
            await WaitFor(() => !data.IsBusy && data.StatusMessage == "Значения сохранены");
            Assert.False(data.HasUnsavedChanges);
            data.OutputFileNameTemplate += "_новое имя";
            Assert.True(data.HasUnsavedChanges);
            Click(window, saveValues);
            await WaitFor(() => !data.IsBusy && data.StatusMessage == "Значения сохранены");
            Assert.True(data.HasUnsavedChanges); // Values-only cannot acknowledge unpersisted output metadata.
            data.OutputFileNameTemplate = "Договор_заполненный";
            data.OutputDirectory = Path.Combine(directory, "другой каталог");
            Assert.True(data.HasUnsavedChanges);
        }
        finally { window.Close(); Directory.Delete(directory, true); }
    }

    private static async Task WaitFor(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(5);
        Assert.True(predicate());
    }

    private static void Click(Window window, Button button)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();
        Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0);
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
    }
    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("DOCTEMPLATER_UI_ARTIFACTS");
        if (directory == null) return;
        Directory.CreateDirectory(directory); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(directory, name + ".png"));
    }
    private sealed class ReadyAdapter : IApplicationUpdateAdapter
    {
        public bool IsInstalled => true;
        public string Version => "0.1.0";
        public bool HasPendingUpdate => true;
        public int Applies;
        public Task<bool> CheckAsync() => Task.FromResult(false);
        public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public void ApplyAndRestart() => Applies++;
    }

    private sealed class DownloadAdapter : IApplicationUpdateAdapter
    {
        public bool IsInstalled => true;
        public string Version => "0.1.0";
        public bool HasPendingUpdate => false;
        public TaskCompletionSource Download { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> CheckAsync() => Task.FromResult(true);
        public Task DownloadAsync(Action<int> progress, CancellationToken cancellationToken) { progress(57); return Download.Task; }
        public void ApplyAndRestart() => throw new InvalidOperationException();
    }
}
