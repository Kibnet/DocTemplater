using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DocTemplater.Views;

public partial class OverwriteConfirmationDialog : Window
{
    public OverwriteConfirmationDialog()
        : this(string.Empty)
    {
    }

    public OverwriteConfirmationDialog(string outputPath)
    {
        InitializeComponent();
        this.FindControl<TextBlock>("OverwriteFilePathTextBlock")!.Text = outputPath;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnOverwriteClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
