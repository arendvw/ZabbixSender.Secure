namespace ZabbixSender.Template.Tests.Fixtures;

public class AppMetrics
{
    public int SignedIn { get; set; }
    public Dictionary<string, HealthCheckStatus> Health { get; set; } = new();
}
