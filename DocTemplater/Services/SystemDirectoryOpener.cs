using System.Diagnostics;

namespace DocTemplater.Services;

public sealed class SystemDirectoryOpener : IDirectoryOpener
{
    public void Open(string directory)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
