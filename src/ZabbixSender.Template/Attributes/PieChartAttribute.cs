namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Class)]
public class PieChartAttribute(string title) : Attribute
{
    public string Title { get; } = title;
}
