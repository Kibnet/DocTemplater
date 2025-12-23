using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DocTemplater.ViewModels;

namespace DocTemplater.Views;

public partial class TemplateSelectionView : UserControl
{
    public TemplateSelectionView()
    {
        InitializeComponent();
        
        // Подписаться на изменение DataContext
        DataContextChanged += OnDataContextChanged;
        
        // Подписаться на команду изменения директории
        this.Loaded += (s, e) =>
        {
            if (DataContext is TemplateSelectionViewModel vm)
            {
                vm.ChangeTemplatesDirectoryCommand.Subscribe(async _ => await vm.ChangeTemplatesDirectoryAsync());
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TemplateSelectionViewModel viewModel)
        {
            // Установить функцию для выбора директории
            viewModel.BrowseForFolderAsync = BrowseForFolderAsync;
        }
    }

    private async Task<string?> BrowseForFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите директорию для шаблонов",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
