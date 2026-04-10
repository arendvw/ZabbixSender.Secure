namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Property)]
public class DiscoveryAttribute(string macroName) : Attribute
{
    public string MacroName { get; } = macroName;
}
