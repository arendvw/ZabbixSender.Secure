namespace ZabbixSender.Template.Model;

public class Trigger
{
    public required string Uuid { get; set; }
    public required string Expression { get; set; }
    public required string Name { get; set; }
    public required string Priority { get; set; }
    public string? Description { get; set; }
    public string? RecoveryExpression { get; set; }
    public List<(string Tag, string Value)> Tags { get; set; } = [];
}
