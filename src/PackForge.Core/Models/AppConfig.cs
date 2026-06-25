namespace PackForge.Core;

public sealed class AppConfig
{
    public Dictionary<string, bool> ManagerVisibility { get; set; } = new();
    public string Theme { get; set; } = "GruvboxDark";
}
