using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DocTemplater.Views;

public partial class TemplateSelectionView : UserControl
{
    public TemplateSelectionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
