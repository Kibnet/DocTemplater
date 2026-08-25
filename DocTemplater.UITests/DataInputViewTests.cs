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

    [AvaloniaFact]
    public async Task PlaceholderFields_ShowInsertTokenButtonsForAllEditors()
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

                Assert.Single(FindInsertTokenButtons(view, dateField));
                Assert.Single(FindInsertTokenButtons(view, textField));
                Assert.Single(FindInsertTokenButtons(view, integerField));
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    [AvaloniaFact]
    public async Task InsertPlaceholderTokenButton_ClickInsertsTokenAtTemplateCaret()
    {
        var textField = CreateField("Имя", PlaceholderDataType.String);
        Window? window = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var view = CreateView(textField);
                var viewModel = Assert.IsType<DataInputViewModel>(view.DataContext);
                viewModel.OutputFileNameTemplate = "StartEnd";
                window = ShowInWindow(view);

                var templateTextBox = FindNamedControl<TextBox>(view, "OutputFileNameTemplateTextBox");
                templateTextBox.Focus();
                templateTextBox.CaretIndex = 5;
                templateTextBox.SelectionStart = 5;
                templateTextBox.SelectionEnd = 5;
                Dispatcher.UIThread.RunJobs();

                var insertButton = FindInsertTokenButtons(view, textField).Single();
                Assert.NotNull(insertButton.Command);

                insertButton.Command.Execute(insertButton.CommandParameter);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("Start{Имя}End", viewModel.OutputFileNameTemplate);
                Assert.Equal("Start{Имя}End", templateTextBox.Text);
                Assert.Equal("Start{Имя}".Length, templateTextBox.CaretIndex);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
        }
    }

    [AvaloniaFact]
    public async Task SaveSettingsPanel_ControlsRemainVisibleAtSupportedWidths()
    {
        foreach (var width in new[] { 800d, 900d })
        {
            Window? window = null;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var view = CreateView(CreateField("Имя", PlaceholderDataType.String));
                    window = ShowInWindow(view, width, 600);

                    var saveSettingsPanel = FindNamedControl<StackPanel>(view, "SaveSettingsPanel");
                    var templateTextBox = FindNamedControl<TextBox>(view, "OutputFileNameTemplateTextBox");
                    var outputPathTextBox = FindNamedControl<TextBox>(view, "OutputFilePathTextBox");
                    var browseButton = FindNamedControl<Button>(view, "BrowseOutputPathButton");

                    Assert.True(saveSettingsPanel.IsVisible);
                    Assert.True(templateTextBox.IsVisible);
                    Assert.True(outputPathTextBox.IsVisible);
                    Assert.True(browseButton.IsVisible);
                    Assert.True(templateTextBox.Bounds.Width >= 200);
                    Assert.True(outputPathTextBox.Bounds.Width >= 200);
                    Assert.True(browseButton.Bounds.Width > 0);
                });
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => window?.Close());
            }
        }
    }

    [AvaloniaFact]
    public async Task DataContext_AssignsOverwriteConfirmationHandler()
    {
        Window? window = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var view = CreateView(CreateField("Имя", PlaceholderDataType.String));
                var viewModel = Assert.IsType<DataInputViewModel>(view.DataContext);
                window = ShowInWindow(view);

                Assert.NotNull(viewModel.ConfirmFileOverwriteAsync);
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

    private static Window ShowInWindow(DataInputView view, double width = 900, double height = 600)
    {
        var window = new Window
        {
            Content = view,
            Width = width,
            Height = height
        };

        window.Show();
        view.Measure(new Size(width, height));
        view.Arrange(new Rect(0, 0, width, height));
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

    private static List<Button> FindInsertTokenButtons(DataInputView view, PlaceholderFieldViewModel field)
    {
        return view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => ReferenceEquals(button.DataContext, field) &&
                             button.Name == "InsertPlaceholderTokenButton")
            .ToList();
    }

    private static T FindNamedControl<T>(DataInputView view, string name)
        where T : Control
    {
        return view.GetVisualDescendants()
            .OfType<T>()
            .Single(control => control.Name == name);
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
            string? lastSaveDirectory = null,
            string? outputFileNameTemplate = null)
        {
            return Task.CompletedTask;
        }
    }
}
