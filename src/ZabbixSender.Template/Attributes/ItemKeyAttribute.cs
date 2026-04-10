namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Property)]
public class ItemKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
