namespace ZabbixSender.Template.Tests.Fixtures;

public class HealthCheckStatus
{
    [Trigger("Degraded", Priority.Warning)]
    [Trigger("Unhealthy", Priority.High)]
    public string Status { get; set; } = "";
    public string Description { get; set; } = "";
}
