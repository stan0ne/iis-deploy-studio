namespace IISDeploy.Application.DTOs;

public class CredentialEntry
{
    public string Identifier { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public bool RequiresPassword { get; set; } = true;
    public string? Description { get; set; }
}
