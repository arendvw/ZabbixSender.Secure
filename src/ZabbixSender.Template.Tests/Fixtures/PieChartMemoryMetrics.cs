namespace ZabbixSender.Template.Tests.Fixtures;

[PieChart("Memory Distribution")]
public class PieChartMemoryMetrics
{
    [Units("B")]
    public int App { get; set; }
    [Units("B")]
    public int Postgres { get; set; }
    [Units("B")]
    public int Free { get; set; }
}
