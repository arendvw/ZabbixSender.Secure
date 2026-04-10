namespace ZabbixSender.Template.Model;

public class MasterItem
{
    public required string Uuid { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public string? Description { get; set; }
}
