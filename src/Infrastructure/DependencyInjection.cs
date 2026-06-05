using IISDeploy.Application.Services;
using IISDeploy.Core.Interfaces;
using IISDeploy.Infrastructure.DependencyScanning;
using IISDeploy.Infrastructure.Iis;
using IISDeploy.Infrastructure.Logging;
using IISDeploy.Infrastructure.Packaging;
using IISDeploy.Infrastructure.Plugins;
using IISDeploy.Infrastructure.PowerShell;
using IISDeploy.Infrastructure.Reporting;
using IISDeploy.Infrastructure.Security;
using IISDeploy.Infrastructure.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace IISDeploy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IIisDiscoveryService, IisDiscoveryService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<ILoggingService, ConsoleLoggingService>();
        services.AddSingleton<IPackageBuilderService, ZipPackageBuilderService>();
        services.AddSingleton<IIisExportService, IisExportService>();
        services.AddSingleton<IIisImportService, IisImportService>();
        services.AddSingleton<IBindingManagerService, BindingManagerService>();
        services.AddSingleton<IConflictResolutionService, ConflictResolutionService>();
        services.AddSingleton<IDependencyScannerService, DependencyScannerService>();
        services.AddSingleton<RuntimeDetectionService>();
        services.AddSingleton<IisFeatureScanner>();
        services.AddSingleton<ThirdPartyScanner>();
        services.AddSingleton<IPowerShellExecutionService, PowerShellExecutionService>();
        services.AddSingleton<IReportGeneratorService, ReportGeneratorService>();
        services.AddSingleton<ICertificateExportService, CertificateExportService>();
        services.AddSingleton<TransactionManager>();
        services.AddSingleton<RollbackService>();
        services.AddSingleton<ResumableExportService>();
        services.AddSingleton<ITransactionManager, TransactionManagerAdapter>();
        services.AddSingleton<PluginHost>();

        return services;
    }
}
