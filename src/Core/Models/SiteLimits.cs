namespace IISDeploy.Core.Models;

public class SiteLimits
{
    public long MaxBandwidth { get; set; }
    public long MaxConnections { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }
}
