namespace EduTwin.BLL.Platform;

public sealed class PlatformBootstrapOptions
{
    public const string SectionName = "PlatformBootstrap";

    public string AdminUsername { get; set; } = "platform.admin";
    public string AdminDisplayName { get; set; } = "Platform Administrator";
    public string? AdminPassword { get; set; }
}
