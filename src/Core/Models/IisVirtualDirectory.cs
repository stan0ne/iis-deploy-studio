namespace IISDeploy.Core.Models;

public class IisVirtualDirectory
{
    public string Path { get; set; } = "/";
    public string PhysicalPath { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? LogonMethod { get; set; }
}
