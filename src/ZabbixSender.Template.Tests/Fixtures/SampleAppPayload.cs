namespace ZabbixSender.Template.Tests.Fixtures;

public class SampleAppPayload
{
    public MemoryMetrics Memory { get; set; } = new();
    public UsersMetrics Users { get; set; } = new();
    public Dictionary<string, HealthCheckStatus> Health { get; set; } = new();
}
