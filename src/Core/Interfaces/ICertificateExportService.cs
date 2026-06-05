using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface ICertificateExportService
{
    Task<List<CertificateInfo>> GetCertificatesAsync();
    Task<byte[]> ExportCertificateAsync(string thumbprint, string password);
    Task ImportCertificateAsync(byte[] pfxData, string password, string storeName);
    Task<bool> CertificateExistsAsync(string thumbprint);
}
