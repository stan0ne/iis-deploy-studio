using IISDeploy.Application.Implementations;
using IISDeploy.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IISDeploy.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<IExportOrchestrator, ExportOrchestrator>();
        services.AddTransient<IImportOrchestrator, ImportOrchestrator>();
        services.AddTransient<IDependencyAnalysisService, DependencyAnalysisService>();

        return services;
    }
}
