using ZabbixSender.Template;

namespace ZabbixSender.Template.Tests.Fixtures;

[PieChart("Memory distribution")]
public class MemoryMetrics
{
    [Units("B")]
    public int App { get; set; }
    [Units("B")]
    public int Postgres { get; set; }
    [Units("B")]
    public int Free { get; set; }
}
