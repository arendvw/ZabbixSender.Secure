namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Property)]
public class UnitsAttribute(string units) : Attribute
{
    public string Units { get; } = units;
}
