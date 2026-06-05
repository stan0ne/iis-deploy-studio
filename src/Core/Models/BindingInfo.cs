namespace IISDeploy.Core.Models;

public class BindingInfo
{
    public string Protocol { get; set; } = "http";
    public string BindingInformation { get; set; } = string.Empty;
    public string? Host { get; set; }
    public string? IpAddress { get; set; }
    public string Port { get; set; } = "80";
    public string? CertificateStoreName { get; set; }
    public string? CertificateHash { get; set; }
    public bool RequireServerNameIndication { get; set; }
    public string? SslFlags { get; set; }
}
