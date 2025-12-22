using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DocTemplater.Views;

public partial class DataInputView : UserControl
{
    public DataInputView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
