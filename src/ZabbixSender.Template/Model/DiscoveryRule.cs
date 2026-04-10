namespace ZabbixSender.Template.Model;

public class DiscoveryRule
{
    public required string Uuid { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public required string MasterItemKey { get; set; }
    public required string MacroName { get; set; }
    public required string MacroPath { get; set; }
    public required string JsPreprocessing { get; set; }
    public required string CollectionJsonProperty { get; set; }
    public List<ItemPrototype> ItemPrototypes { get; set; } = [];
    public List<TriggerPrototype> TriggerPrototypes { get; set; } = [];
}
