using System.Runtime.InteropServices;

namespace PackForge.Tui;

internal static class SystemInfo
{
    internal static (double fraction, string label) DiskUsage()
    {
        try
        {
            var drive = new DriveInfo("/");
            var total = drive.TotalSize;
            if (total <= 0) return (0, "—");
            var used = total - drive.AvailableFreeSpace;
            var fraction = Math.Clamp((double)used / total, 0.0, 1.0);
            var pct = (int)Math.Round(fraction * 100);
            return (fraction, $"{pct}% FULL");
        }
        catch
        {
            return (0, "—");
        }
    }

    internal static string Architecture()
    {
        try
        {
            return RuntimeInformation.OSArchitecture.ToString();
        }
        catch
        {
            return "—";
        }
    }

    internal static string Prompt()
    {
        try
        {
            var user = Environment.UserName;
            var host = Environment.MachineName;
            var cwd = Environment.CurrentDirectory;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                cwd = "~" + cwd[home.Length..];
            return $"{user}@{host} : {cwd}";
        }
        catch
        {
            return "user@host : ~";
        }
    }

    internal static string? BinaryModified(string command)
    {
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            proc.Start();
            var path = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(2000);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            return File.GetLastWriteTime(path).ToString("yyyy.MM.dd");
        }
        catch
        {
            return null;
        }
    }
}
