namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Class)]
public class ZabbixPrefixAttribute(string prefix) : Attribute
{
    public string Prefix { get; } = prefix;
}
