namespace ZabbixSender.Template;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class TriggerAttribute : Attribute
{
    /// <summary>
    /// Trigger on exact string match: last(key)="value"
    /// </summary>
    public TriggerAttribute(string value, Priority priority)
    {
        Value = value;
        Operator = "=";
        Priority = priority;
    }

    /// <summary>
    /// Trigger on numeric comparison: last(key) op threshold
    /// Supported operators: ">", "<", ">=", "<=", "=", "<>"
    /// </summary>
    public TriggerAttribute(string op, double threshold, Priority priority)
    {
        Operator = op;
        Threshold = threshold;
        Priority = priority;
    }

    public string? Value { get; }
    public string Operator { get; }
    public double? Threshold { get; }
    public Priority Priority { get; }

    /// <summary>
    /// Duration the condition must hold before the trigger fires.
    /// Uses Zabbix time suffixes: "5m", "1h", "30s", etc.
    /// When set, uses min/max instead of last depending on the operator.
    /// </summary>
    public string? Duration { get; set; }

    /// <summary>
    /// Auto-resolve after this duration once the condition clears.
    /// Uses Zabbix time suffixes. Sets a recovery expression using nodata or
    /// the inverse condition over the recovery period.
    /// Example: "5m" means the trigger resolves after the value returns to
    /// normal for 5 minutes.
    /// </summary>
    public string? Recovery { get; set; }
}

/// <summary>
/// Trigger when the value changes. Fires on change, auto-resolves after
/// the recovery period (default: immediately on next unchanged value).
/// Useful for restart detection, version changes, config reloads, etc.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class TriggerOnChangeAttribute(Priority priority) : Attribute
{
    public Priority Priority { get; } = priority;

    /// <summary>
    /// How long to keep the trigger active after the change.
    /// Uses Zabbix time suffixes: "5m", "1h", etc.
    /// Default: no duration (resolves when the value stops changing).
    /// </summary>
    public string? Duration { get; set; }
}
