using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DocTemplater.ViewModels;

namespace DocTemplater.Views;

public partial class DataInputView : UserControl
{
    private TextBox? _outputFileNameTemplateTextBox;
    private DataInputViewModel? _attachedViewModel;

    public DataInputView()
    {
        InitializeComponent();
        _outputFileNameTemplateTextBox = this.FindControl<TextBox>("OutputFileNameTemplateTextBox");
        if (_outputFileNameTemplateTextBox != null)
        {
            _outputFileNameTemplateTextBox.PropertyChanged += (_, _) => UpdateOutputFileNameTemplateSelection();
        }
        
        // Подписаться на изменение DataContext
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_attachedViewModel != null)
        {
            _attachedViewModel.OutputFileNameTemplateFocusRequested -= OnOutputFileNameTemplateFocusRequested;
        }

        if (DataContext is DataInputViewModel viewModel)
        {
            // Установить функцию для открытия диалога сохранения файла
            viewModel.BrowseForSaveFileAsync = BrowseForSaveFileAsync;
            viewModel.OutputFileNameTemplateFocusRequested += OnOutputFileNameTemplateFocusRequested;
            _attachedViewModel = viewModel;
            UpdateOutputFileNameTemplateSelection();
        }
        else
        {
            _attachedViewModel = null;
        }
    }

    private void UpdateOutputFileNameTemplateSelection()
    {
        if (_outputFileNameTemplateTextBox == null || DataContext is not DataInputViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateOutputFileNameTemplateSelection(
            _outputFileNameTemplateTextBox.CaretIndex,
            _outputFileNameTemplateTextBox.SelectionStart,
            _outputFileNameTemplateTextBox.SelectionEnd);
    }

    private void OnOutputFileNameTemplateFocusRequested(int caretIndex)
    {
        if (_outputFileNameTemplateTextBox == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var textLength = _outputFileNameTemplateTextBox.Text?.Length ?? 0;
            var clampedCaretIndex = Math.Clamp(caretIndex, 0, textLength);

            _outputFileNameTemplateTextBox.Focus();
            _outputFileNameTemplateTextBox.CaretIndex = clampedCaretIndex;
            _outputFileNameTemplateTextBox.SelectionStart = clampedCaretIndex;
            _outputFileNameTemplateTextBox.SelectionEnd = clampedCaretIndex;
            UpdateOutputFileNameTemplateSelection();
        });
    }

    private async Task<string?> BrowseForSaveFileAsync(string startingDirectory)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return null;

        var viewModel = DataContext as DataInputViewModel;
        var suggestedFileName = viewModel?.OutputFileName ?? "document";
        
        // Попытаться получить стартовую директорию из текущего OutputDirectory
        var directoryToUse = viewModel?.OutputDirectory ?? startingDirectory;
        var suggestedDirectory = string.IsNullOrEmpty(directoryToUse) || !Directory.Exists(directoryToUse)
            ? null
            : await topLevel.StorageProvider.TryGetFolderFromPathAsync(directoryToUse);

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Выберите место для сохранения документа",
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = suggestedDirectory,
            DefaultExtension = "docx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Документы Word")
                {
                    Patterns = new[] { "*.docx" },
                    MimeTypes = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
                }
            }
        });

        return file?.Path.LocalPath;
    }
}
