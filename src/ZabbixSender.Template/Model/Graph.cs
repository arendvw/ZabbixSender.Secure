namespace ZabbixSender.Template.Model;

public enum GraphType
{
    SingleLine,
    StackedLine,
    PieChart
}

public class Graph
{
    public required string Uuid { get; set; }
    public required string Name { get; set; }
    public required GraphType Type { get; set; }
    public List<GraphItem> Items { get; set; } = [];
}

public class GraphItem
{
    public required string ItemKey { get; set; }
    public required string Color { get; set; }
}
