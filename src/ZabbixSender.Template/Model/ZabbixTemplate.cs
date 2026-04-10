namespace ZabbixSender.Template.Model;

public class ZabbixTemplate
{
    public required string TemplateName { get; set; }
    public required string Prefix { get; set; }
    public required string TemplateGroupUuid { get; set; }
    public required string TemplateUuid { get; set; }
    public string? Description { get; set; }
    public required MasterItem MasterItem { get; set; }
    public List<DependentItem> DependentItems { get; set; } = [];
    public List<DiscoveryRule> DiscoveryRules { get; set; } = [];
    public List<Trigger> Triggers { get; set; } = [];
    public List<Graph> Graphs { get; set; } = [];

    public string ToYaml() => YamlSerializer.ToYaml(this);
}
