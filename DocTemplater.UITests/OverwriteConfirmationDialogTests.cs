using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DocTemplater.Views;

namespace DocTemplater.UITests;

public class OverwriteConfirmationDialogTests
{
    [AvaloniaFact]
    public async Task Dialog_ShowsTargetPathAndExplicitActions()
    {
        const string outputPath = @"C:\Output\Existing document.docx";
        Window? owner = null;
        OverwriteConfirmationDialog? dialog = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = new Window();
                owner.Show();
                dialog = new OverwriteConfirmationDialog(outputPath);
                _ = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("Файл уже существует", dialog.Title);
                Assert.Equal(outputPath,
                    dialog.FindControl<TextBlock>("OverwriteFilePathTextBlock")?.Text);
                Assert.Equal("Отмена",
                    dialog.FindControl<Button>("CancelOverwriteButton")?.Content);
                Assert.Equal("Перезаписать",
                    dialog.FindControl<Button>("OverwriteButton")?.Content);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close(false);
                owner?.Close();
            });
        }
    }

    [AvaloniaFact]
    public async Task CancelButton_ReturnsFalse()
    {
        Window? owner = null;
        OverwriteConfirmationDialog? dialog = null;
        Task<bool>? resultTask = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = new Window();
                owner.Show();
                dialog = new OverwriteConfirmationDialog(@"C:\Output\Existing.docx");
                resultTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                var cancelButton = dialog.FindControl<Button>("CancelOverwriteButton");
                Assert.NotNull(cancelButton);
                cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
            });

            Assert.NotNull(resultTask);
            Assert.False(await resultTask);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close(false);
                owner?.Close();
            });
        }
    }

    [AvaloniaFact]
    public async Task OverwriteButton_ReturnsTrue()
    {
        Window? owner = null;
        OverwriteConfirmationDialog? dialog = null;
        Task<bool>? resultTask = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = new Window();
                owner.Show();
                dialog = new OverwriteConfirmationDialog(@"C:\Output\Existing.docx");
                resultTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                var overwriteButton = dialog.FindControl<Button>("OverwriteButton");
                Assert.NotNull(overwriteButton);
                overwriteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
            });

            Assert.NotNull(resultTask);
            Assert.True(await resultTask);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close(false);
                owner?.Close();
            });
        }
    }

    [AvaloniaFact]
    public async Task EscapeKey_ReturnsFalse()
    {
        Window? owner = null;
        OverwriteConfirmationDialog? dialog = null;
        Task<bool>? resultTask = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = new Window();
                owner.Show();
                dialog = new OverwriteConfirmationDialog(@"C:\Output\Existing.docx");
                resultTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                dialog.KeyPress(
                    Key.Escape,
                    RawInputModifiers.None,
                    PhysicalKey.Escape,
                    "\u001b");
                Dispatcher.UIThread.RunJobs();
            });

            Assert.NotNull(resultTask);
            Assert.False(await resultTask);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close(false);
                owner?.Close();
            });
        }
    }

    [AvaloniaFact]
    public async Task ClosingWindow_ReturnsFalse()
    {
        Window? owner = null;
        OverwriteConfirmationDialog? dialog = null;
        Task<bool>? resultTask = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = new Window();
                owner.Show();
                dialog = new OverwriteConfirmationDialog(@"C:\Output\Existing.docx");
                resultTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();

                dialog.Close();
                Dispatcher.UIThread.RunJobs();
            });

            Assert.NotNull(resultTask);
            Assert.False(await resultTask);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog?.Close(false);
                owner?.Close();
            });
        }
    }
}
