using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Automation;

// External driver only: all check/download/confirmation/apply operations run through the actual packaged UI.
internal static class Program
{
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [MTAThread]
    private static int Main(string[] args)
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
            var processName = args[0]; var action = args[1]; var output = args.Length > 2 ? args[2] : ".";
            Directory.CreateDirectory(output);
            var process = Wait(() => processName.StartsWith('#') ? Process.GetProcessById(int.Parse(processName[1..]))
                : Process.GetProcessesByName(processName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero), "window");
            var window = AutomationElement.FromHandle(process.MainWindowHandle);
            if (action == "close") { ((WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern)).Close(); return 0; }
            if (action == "inspect")
            {
                Dump(window, output); Capture(process, Path.Combine(output, "native.png")); return 0;
            }
            if (action == "ready")
            {
                Wait(() => Find(window, "Установить и перезапустить"), "ready package");
                Dump(window, output); Capture(process, Path.Combine(output, "ready.png")); return 0;
            }
            if (action == "apply")
            {
                var button = Wait(() => Find(window, "Установить и перезапустить"), "apply button");
                Invoke(button); return 0;
            }
            if (action == "check") { Invoke(Wait(() => Find(window, "Проверить обновления"), "check button")); return 0; }
            if (action == "select")
            {
                if (FindById(window, "ValueTextBox") == null)
                {
                    var list = Wait(() => FindById(window, "TemplatesListBox"), "templates list");
                    var item = Wait(() => list.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)), "template item");
                    ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                    Invoke(Wait(() => FindById(window, "SelectTemplateButton"), "select button"));
                }
                var field = Wait(() => FindById(window, "ValueTextBox"), "input field");
                ((ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern)).SetValue("Unsaved native edit");
                Dump(window, output); Capture(process, Path.Combine(output, "dirty-form.png")); return 0;
            }
            if (action == "cancel" || action == "confirm")
            {
                AutomationElement? FindDialog()
                {
                    var handle = FindWindow(null, "Несохранённые изменения");
                    return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
                }
                Task? invoke = null;
                if (FindDialog() == null)
                {
                    var apply = Wait(() => Find(window, "Установить и перезапустить"), "apply button");
                    // Avalonia's synchronous UIA Invoke remains on the dialog's nested message loop until it closes.
                    invoke = Task.Run(() => Invoke(apply));
                }
                var dialog = Wait(FindDialog, "dirty confirmation");
                Dump(dialog, output);
                try
                {
                    Invoke(Wait(() => Find(dialog, action == "cancel" ? "Вернуться к документу" : "Обновить без сохранения"), "confirmation choice"));
                    invoke?.Wait(TimeSpan.FromSeconds(10));
                }
                catch (Exception) when (action == "confirm") { /* Verify process/version change separately; the old UIA provider exits. */ }
                if (action == "cancel")
                {
                    var field = Wait(() => FindById(window, "ValueTextBox"), "preserved field");
                    if (((ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern)).Current.Value != "Unsaved native edit") throw new Exception("Cancel lost unsaved input.");
                    Capture(process, Path.Combine(output, "after-cancel.png"));
                }
                return 0;
            }
            throw new ArgumentException("Unknown action");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static AutomationElement? Find(AutomationElement root, string name) => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
    private static AutomationElement? FindById(AutomationElement root, string id) => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));
    private static T Wait<T>(Func<T?> get, string description) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline) { var result = get(); if (result != null) return result; Thread.Sleep(200); }
        throw new TimeoutException(description);
    }
    private static void Invoke(AutomationElement button) => ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    private static void Dump(AutomationElement root, string directory)
    {
        var lines = root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>()
            .Select(e => $"{e.Current.ControlType.ProgrammaticName}\t{e.Current.AutomationId}\t{e.Current.Name}\tEnabled={e.Current.IsEnabled}");
        File.WriteAllLines(Path.Combine(directory, "uia.txt"), lines);
    }
    private static void Capture(Process process, string filename)
    {
        SetForegroundWindow(process.MainWindowHandle);
        var rect = AutomationElement.FromHandle(process.MainWindowHandle).Current.BoundingRectangle;
        using var bitmap = new Bitmap((int)rect.Width, (int)rect.Height);
        using var graphics = Graphics.FromImage(bitmap);
        Thread.Sleep(350);
        graphics.CopyFromScreen((int)rect.Left, (int)rect.Top, 0, 0, bitmap.Size);
        bitmap.Save(filename, ImageFormat.Png);
    }
}
