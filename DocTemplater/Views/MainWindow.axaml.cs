using Avalonia.Controls;
using DocTemplater.ViewModels;

namespace DocTemplater.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) vm.ConfirmDiscardForUpdateAsync = ConfirmDiscardAsync;
        };
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        var dialog = new Window { Title = "Несохранённые изменения", Width = 520, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var cancel = new Button { Content = "Вернуться к документу", IsDefault = true, IsCancel = true, Name = "CancelUpdateButton" };
        var apply = new Button { Content = "Обновить без сохранения", Name = "ConfirmUpdateButton" };
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 16, Children =
        {
            new TextBlock { Text = "В форме есть несохранённые значения или настройки выходного файла. При перезапуске они будут потеряны.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            new StackPanel { Spacing = 8, Children = { cancel, apply } }
        }};
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<bool>(this);
    }
}
