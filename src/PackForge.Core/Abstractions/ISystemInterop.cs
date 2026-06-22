namespace PackForge.Core;

public interface ISystemInterop
{
    void OpenUrl(string url);
    Task CopyToClipboardAsync(string text);
}
