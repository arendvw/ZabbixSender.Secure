namespace ZabbixSender.Template.Tests.Fixtures;

public class TestPayload
{
    public UsersMetrics Users { get; set; } = new();
    public Dictionary<string, HealthCheckStatus> Health { get; set; } = new();
}
