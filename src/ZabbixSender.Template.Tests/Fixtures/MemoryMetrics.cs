namespace ZabbixSender.Template.Tests.Fixtures;

public class MemoryMetrics
{
    [Units("MB")]
    public int App { get; set; }
    [Units("MB")]
    public int Postgres { get; set; }
    [Units("MB")]
    public int Free { get; set; }
}
