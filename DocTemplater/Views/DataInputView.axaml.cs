using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DocTemplater.ViewModels;

namespace DocTemplater.Views;

public partial class DataInputView : UserControl
{
    public DataInputView()
    {
        InitializeComponent();
        
        // Подписаться на изменение DataContext
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DataInputViewModel viewModel)
        {
            // Установить функцию для открытия диалога сохранения файла
            viewModel.BrowseForSaveFileAsync = BrowseForSaveFileAsync;
        }
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
