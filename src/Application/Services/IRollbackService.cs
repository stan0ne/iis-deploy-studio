namespace IISDeploy.Application.Services;

public interface IRollbackService
{
    Task<T> CreateSnapshotAsync<T>() where T : class, new();
    Task<bool> RollbackImportAsync<T>(T snapshot, List<string> importedSiteNames, List<string> importedPoolNames) where T : class;
}
