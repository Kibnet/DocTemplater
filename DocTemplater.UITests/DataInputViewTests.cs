using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocTemplater.Domain;
using DocTemplater.Services;
using DocTemplater.ViewModels;
using DocTemplater.Views;
using Xunit;

namespace DocTemplater.UITests;

public class DataInputViewTests
{
    [AvaloniaFact]
    public async Task DateField_ShowsQuickAddButtonsOnlyForDateEditors()
    {
        var dateField = CreateField("Дата", PlaceholderDataType.DateTimeOffset);
        var textField = CreateField("Имя", PlaceholderDataType.String);
        var integerField = CreateField("Возраст", PlaceholderDataType.Integer);
        Window? window = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var view = CreateView(dateField, textField, integerField);
                window = ShowInWindow(view);

                var dateButtons = FindButtons(view, dateField);
                var textButtons = FindButtons(view, textField);
                var integerButtons = FindButtons(view, integerField);
                var panels = view.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Where(panel => panel.Name == "DateQuickButtonsPanel")
                    .ToList();

                Assert.Contains(dateButtons, button => button.Name == "AddDayButton");
                Assert.Contains(dateButtons, button => button.Name == "AddMonthButton");
                Assert.Contains(dateButtons, button => button.Name == "AddYearButton");
                Assert.Contains(view.GetVisualDescendants().OfType<DatePicker>(),
                    picker => picker.Name == "DateValuePicker" && ReferenceEquals(picker.DataContext, dateField));

                Assert.True(panels.Single(panel => ReferenceEquals(panel.DataContext, dateField)).IsVisible);
                Assert.False(panels.Single(panel => ReferenceEquals(panel.DataContext, textField)).IsVisible);
                Assert.False(panels.Single(panel => ReferenceEquals(panel.DataContext, integerField)).IsVisible);
                Assert.Equal(3, textButtons.Count);
                Assert.Equal(3, integerButtons.Count);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    [AvaloniaFact]
    public async Task AddDayButton_ClickUpdatesDateValue()
    {
        var dateField = CreateField("Дата", PlaceholderDataType.DateTimeOffset);
        var initialValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));
        dateField.DateValue = initialValue;
        Window? window = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var view = CreateView(dateField);
                window = ShowInWindow(view);

                var addDayButton = FindButtons(view, dateField).Single(button => button.Name == "AddDayButton");
                Assert.NotNull(addDayButton.Command);

                addDayButton.Command.Execute(addDayButton.CommandParameter);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(initialValue.AddDays(1), dateField.DateValue);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    private static DataInputView CreateView(params PlaceholderFieldViewModel[] fields)
    {
        var viewModel = new DataInputViewModel(
            new Template
            {
                FileName = "template.docx",
                FilePath = Path.Combine(Path.GetTempPath(), "template.docx")
            },
            new StubDocumentGenerationService(),
            new StubValuePersistenceService());

        Dispatcher.UIThread.RunJobs();
        viewModel.Fields = fields.ToList();

        return new DataInputView
        {
            DataContext = viewModel
        };
    }

    private static Window ShowInWindow(DataInputView view)
    {
        var window = new Window
        {
            Content = view,
            Width = 900,
            Height = 600
        };

        window.Show();
        view.Measure(new Size(900, 600));
        view.Arrange(new Rect(0, 0, 900, 600));
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static List<Button> FindButtons(DataInputView view, PlaceholderFieldViewModel field)
    {
        return view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => ReferenceEquals(button.DataContext, field) &&
                             button.Name is "AddDayButton" or "AddMonthButton" or "AddYearButton")
            .ToList();
    }

    private static PlaceholderFieldViewModel CreateField(string name, PlaceholderDataType dataType)
    {
        return new PlaceholderFieldViewModel(
            new Placeholder
            {
                Name = name,
                DataType = dataType,
                FormatPatterns = dataType == PlaceholderDataType.DateTimeOffset
                    ? new List<string> { "d.MM.yyyy" }
                    : new List<string>()
            },
            CultureInfo.GetCultureInfo("ru-RU"));
    }

    private sealed class StubDocumentGenerationService : IDocumentGenerationService
    {
        public Task<bool> GenerateDocumentAsync(
            string templatePath,
            string outputPath,
            Dictionary<string, PlaceholderValue> values,
            string cultureName)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class StubValuePersistenceService : IValuePersistenceService
    {
        public Task<AppSettings> LoadSettingsAsync()
        {
            return Task.FromResult(new AppSettings());
        }

        public Task SaveSettingsAsync(AppSettings settings)
        {
            return Task.CompletedTask;
        }

        public Task<SavedTemplateData?> GetSavedValuesAsync(string templateFileName)
        {
            return Task.FromResult<SavedTemplateData?>(null);
        }

        public Task SaveTemplateValuesAsync(
            string templateFileName,
            Dictionary<string, object> values,
            string? lastSaveDirectory = null)
        {
            return Task.CompletedTask;
        }
    }
}
