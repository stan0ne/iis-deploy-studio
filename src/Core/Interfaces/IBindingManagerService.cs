using IISDeploy.Core.Models;

namespace IISDeploy.Core.Interfaces;

public interface IBindingManagerService
{
    Task<List<BindingInfo>> DetectConflictsAsync(List<BindingInfo> bindings);
    Task<bool> IsPortAvailableAsync(string ipAddress, int port);
    Task<string> SuggestAlternativeBindingAsync(BindingInfo binding, List<string> existingBindings);
}
