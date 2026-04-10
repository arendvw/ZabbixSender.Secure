namespace ZabbixSender.Template.Tests.Fixtures;

[PieChart("Memory Distribution")]
public class PieChartMemoryMetrics
{
    [Units("MB")]
    public int App { get; set; }
    [Units("MB")]
    public int Postgres { get; set; }
    [Units("MB")]
    public int Free { get; set; }
}
