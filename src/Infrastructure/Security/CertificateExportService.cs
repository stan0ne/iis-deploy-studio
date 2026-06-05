using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IISDeploy.Core.Interfaces;
using IISDeploy.Core.Models;

namespace IISDeploy.Infrastructure.Security;

public class CertificateExportService : ICertificateExportService
{
    private readonly ILoggingService _logger;

    public CertificateExportService(ILoggingService logger)
    {
        _logger = logger;
    }

    public Task<List<CertificateInfo>> GetCertificatesAsync()
    {
        var certs = new List<CertificateInfo>();

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                certs.Add(MapCertificate(cert));
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Could not read certificate store: {Message}", ex.Message);
        }

        return Task.FromResult(certs);
    }

    public Task<byte[]> ExportCertificateAsync(string thumbprint, string password)
    {
        return Task.Run(() =>
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            var cert = store.Certificates
                .FirstOrDefault(c => string.Equals(c.Thumbprint, thumbprint,
                    StringComparison.OrdinalIgnoreCase));

            if (cert is null)
                throw new InvalidOperationException($"Certificate with thumbprint '{thumbprint}' not found.");

            if (!cert.HasPrivateKey)
                throw new InvalidOperationException("Certificate does not have an exportable private key.");

            return cert.Export(X509ContentType.Pfx, password);
        });
    }

    public Task ImportCertificateAsync(byte[] pfxData, string password, string storeName)
    {
        return Task.Run(() =>
        {
            using var cert = X509CertificateLoader.LoadPkcs12(pfxData, password,
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.Exportable);

            using var store = new X509Store(storeName, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();

            _logger.Information("Certificate imported: {Subject}, thumbprint: {Thumbprint}",
                cert.Subject, cert.Thumbprint);
        });
    }

    public Task<bool> CertificateExistsAsync(string thumbprint)
    {
        return Task.Run(() =>
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            return store.Certificates.Any(c =>
                string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static CertificateInfo MapCertificate(X509Certificate2 cert)
    {
        return new CertificateInfo
        {
            Subject = cert.Subject,
            Issuer = cert.Issuer,
            SerialNumber = cert.SerialNumber,
            Thumbprint = cert.Thumbprint,
            FriendlyName = cert.FriendlyName,
            NotBefore = cert.NotBefore,
            NotAfter = cert.NotAfter,
            HasPrivateKey = cert.HasPrivateKey,
            StoreName = "My"
        };
    }
}
