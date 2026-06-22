using System.Diagnostics;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class SystemInterop : ISystemInterop
{
    /// <summary>Best-effort open of a URL in the default browser. Ignores all errors.</summary>
    public void OpenUrl(string url)
    {
        try
        {
            string fileName, args;
            if (OperatingSystem.IsMacOS())        { fileName = "open";     args = url; }
            else if (OperatingSystem.IsWindows()) { fileName = "cmd";      args = $"/c start \"\" \"{url}\""; }
            else                                  { fileName = "xdg-open"; args = url; }

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch { /* ignore — browser unavailable */ }
    }

    /// <summary>Best-effort copy of text to clipboard via pbcopy (macOS). Ignores all errors.</summary>
    public async Task CopyToClipboardAsync(string text)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pbcopy",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
        }
        catch
        {
            // Ignore — pbcopy unavailable or other error
        }
    }
}
