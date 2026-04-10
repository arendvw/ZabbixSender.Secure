namespace ZabbixSender.Template.Tests.Fixtures;

public class CustomMacroFixture
{
    [Discovery("ENDPOINT")]
    public Dictionary<string, HealthCheckStatus> Endpoints { get; set; } = new();
}
