namespace Bunkum.HttpServer.Health.Reports;

public class GeneralHealthCheck : IHealthCheck
{
    public string Name => "General";

    public HealthStatus RunCheck()
    {
        return new HealthStatus(HealthStatusType.Healthy);
    }
}