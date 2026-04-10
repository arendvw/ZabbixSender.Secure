namespace ZabbixSender.Template;

/// <summary>
/// Send a running total, Zabbix calculates the per-second rate.
/// Adds CHANGE_PER_SECOND preprocessing after the JSONPath extraction.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ChangePerSecondAttribute : Attribute { }
