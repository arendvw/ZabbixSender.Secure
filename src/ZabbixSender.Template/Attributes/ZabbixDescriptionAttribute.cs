namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class ZabbixDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}
