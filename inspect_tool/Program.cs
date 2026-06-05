using Microsoft.Web.Administration;

Console.WriteLine("Testing IIS configuration step by step...");

try
{
    Console.Write("1. ServerManager constructor... ");
    using var sm = new ServerManager();
    Console.WriteLine("OK");

    Console.Write("2. Accessing Sites... ");
    var sites = sm.Sites;
    Console.WriteLine($"OK ({sites.Count} sites)");

    Console.Write("3. Accessing ApplicationPools... ");
    var pools = sm.ApplicationPools;
    Console.WriteLine($"OK ({pools.Count} pools)");

    try
    {
        Console.Write("4. Accessing GlobalModules (reflection)... ");
        var prop = typeof(ServerManager).GetProperty("GlobalModules");
        if (prop is not null)
        {
            Console.Write("property found, getting value... ");
            var val = prop.GetValue(sm);
            Console.WriteLine($"OK (type: {val?.GetType().FullName ?? "null"})");
        }
        else
        {
            Console.WriteLine("Property not available in this version");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }

    // Check each site
    Console.WriteLine("\n5. Iterating sites...");
    foreach (var site in sm.Sites)
    {
        try
        {
            Console.Write($"   Site '{site.Name}' (ID:{site.Id})... ");
            var name = site.Name;
            var state = site.State;
            Console.WriteLine($"OK - State:{state}");

            Console.Write($"     Bindings... ");
            var bindings = site.Bindings;
            Console.WriteLine($"OK ({bindings.Count})");

            Console.Write($"     Applications... ");
            var apps = site.Applications;
            Console.WriteLine($"OK ({apps.Count})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Check each pool
    Console.WriteLine("\n6. Iterating pools...");
    foreach (var pool in sm.ApplicationPools)
    {
        try
        {
            Console.Write($"   Pool '{pool.Name}'... ");
            var pipeline = pool.ManagedPipelineMode;
            var runtime = pool.ManagedRuntimeVersion;
            var identity = pool.ProcessModel.IdentityType;
            var idleTimeout = pool.ProcessModel.IdleTimeout;
            var privateMemory = pool.Recycling.PeriodicRestart.PrivateMemory;
            var memory = pool.Recycling.PeriodicRestart.Memory;
            Console.WriteLine($"OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    Console.WriteLine("\nALL OK - IIS config is healthy");
}
catch (Exception ex)
{
    Console.WriteLine($"\nFAILED at top level: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null)
        Console.WriteLine($"Inner ({ex.InnerException.GetType().Name}): {ex.InnerException.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
