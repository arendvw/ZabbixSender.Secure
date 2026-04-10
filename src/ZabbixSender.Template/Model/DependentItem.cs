namespace ZabbixSender.Template.Model;

public class DependentItem
{
    public required string Uuid { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public required string JsonPath { get; set; }
    public required string MasterItemKey { get; set; }
    public ZabbixValueType ValueType { get; set; } = ZabbixValueType.Unsigned;
    public string? Units { get; set; }
    public string? Description { get; set; }
    public required string Component { get; set; }
}
