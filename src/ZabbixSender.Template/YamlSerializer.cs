using System.Text;
using ZabbixSender.Template.Model;

namespace ZabbixSender.Template;

public static class YamlSerializer
{
    public static string ToYaml(ZabbixTemplate template)
    {
        var sb = new StringBuilder();

        sb.AppendLine("zabbix_export:");
        sb.AppendLine("  version: '7.0'");
        sb.AppendLine("  template_groups:");
        sb.AppendLine($"    - uuid: {template.TemplateGroupUuid}");
        sb.AppendLine("      name: Templates/Applications");
        sb.AppendLine("  templates:");
        sb.AppendLine($"    - uuid: {template.TemplateUuid}");
        sb.AppendLine($"      template: '{template.TemplateName}'");
        sb.AppendLine($"      name: '{template.TemplateName}'");
        if (!string.IsNullOrEmpty(template.Description))
            sb.AppendLine($"      description: '{EscapeSingleQuote(template.Description)}'");
        sb.AppendLine("      groups:");
        sb.AppendLine("        - name: Templates/Applications");

        // Items
        sb.AppendLine("      items:");
        WriteMasterItem(sb, template.MasterItem);
        foreach (var item in template.DependentItems)
        {
            WriteDependentItem(sb, item);
        }

        // Discovery rules
        if (template.DiscoveryRules.Count > 0)
        {
            sb.AppendLine("      discovery_rules:");
            foreach (var rule in template.DiscoveryRules)
            {
                WriteDiscoveryRule(sb, rule, template.TemplateName);
            }
        }

        // Template tags
        sb.AppendLine("      tags:");
        sb.AppendLine("        - tag: class");
        sb.AppendLine("          value: application");
        sb.AppendLine("        - tag: target");
        sb.AppendLine($"          value: {template.Prefix}");

        // Top-level triggers
        if (template.Triggers.Count > 0)
        {
            sb.AppendLine("  triggers:");
            foreach (var trigger in template.Triggers)
            {
                WriteTrigger(sb, trigger, "    ");
            }
        }

        // Top-level graphs
        if (template.Graphs.Count > 0)
        {
            sb.AppendLine("  graphs:");
            foreach (var graph in template.Graphs)
            {
                WriteGraph(sb, graph, template.TemplateName);
            }
        }

        return sb.ToString();
    }

    private static void WriteMasterItem(StringBuilder sb, MasterItem item)
    {
        sb.AppendLine($"        - uuid: {item.Uuid}");
        sb.AppendLine($"          name: '{EscapeSingleQuote(item.Name)}'");
        sb.AppendLine("          type: TRAP");
        sb.AppendLine($"          key: {item.Key}");
        sb.AppendLine("          delay: '0'");
        sb.AppendLine("          history: 1h");
        sb.AppendLine("          trends: '0'");
        sb.AppendLine("          value_type: TEXT");
        if (!string.IsNullOrEmpty(item.Description))
            sb.AppendLine($"          description: '{EscapeSingleQuote(item.Description)}'");
        sb.AppendLine("          tags:");
        sb.AppendLine("            - tag: component");
        sb.AppendLine("              value: raw");
    }

    private static void WriteDependentItem(StringBuilder sb, DependentItem item)
    {
        sb.AppendLine($"        - uuid: {item.Uuid}");
        sb.AppendLine($"          name: '{EscapeSingleQuote(item.Name)}'");
        sb.AppendLine("          type: DEPENDENT");
        sb.AppendLine($"          key: {item.Key}");
        sb.AppendLine("          delay: '0'");
        if (!string.IsNullOrEmpty(item.Units))
            sb.AppendLine($"          units: {item.Units}");
        if (item.ValueType == ZabbixValueType.Char)
        {
            sb.AppendLine("          trends: '0'");
            sb.AppendLine("          value_type: CHAR");
        }
        else if (item.ValueType == ZabbixValueType.Float)
        {
            sb.AppendLine("          value_type: FLOAT");
        }
        else if (item.ValueType == ZabbixValueType.Text)
        {
            sb.AppendLine("          trends: '0'");
            sb.AppendLine("          value_type: TEXT");
        }
        // Unsigned: omit value_type
        if (!string.IsNullOrEmpty(item.Description))
            sb.AppendLine($"          description: '{EscapeSingleQuote(item.Description)}'");
        sb.AppendLine("          preprocessing:");
        sb.AppendLine("            - type: JSONPATH");
        sb.AppendLine("              parameters:");
        sb.AppendLine($"                - {item.JsonPath}");
        foreach (var step in item.ExtraPreprocessing)
        {
            sb.AppendLine($"            - type: {step.Type}");
            if (step.Parameters.Count > 0)
            {
                sb.AppendLine("              parameters:");
                foreach (var param in step.Parameters)
                    sb.AppendLine($"                - '{param}'");
            }
        }
        sb.AppendLine("          master_item:");
        sb.AppendLine($"            key: {item.MasterItemKey}");
        sb.AppendLine("          tags:");
        sb.AppendLine("            - tag: component");
        sb.AppendLine($"              value: {item.Component}");
    }

    private static void WriteDiscoveryRule(StringBuilder sb, DiscoveryRule rule, string templateName)
    {
        sb.AppendLine($"        - uuid: {rule.Uuid}");
        sb.AppendLine($"          name: '{EscapeSingleQuote(rule.Name)}'");
        sb.AppendLine("          type: DEPENDENT");
        sb.AppendLine($"          key: {rule.Key}");
        sb.AppendLine("          delay: '0'");

        // Item prototypes
        if (rule.ItemPrototypes.Count > 0)
        {
            sb.AppendLine("          item_prototypes:");
            foreach (var proto in rule.ItemPrototypes)
            {
                WriteItemPrototype(sb, proto);
            }
        }

        // Trigger prototypes
        if (rule.TriggerPrototypes.Count > 0)
        {
            sb.AppendLine("          trigger_prototypes:");
            foreach (var tp in rule.TriggerPrototypes)
            {
                WriteTriggerPrototype(sb, tp, templateName);
            }
        }

        // LLD macro paths
        sb.AppendLine("          lld_macro_paths:");
        sb.AppendLine($"            - lld_macro: '{rule.MacroName}'");
        sb.AppendLine($"              path: {rule.MacroPath}");

        // Preprocessing: JavaScript + DISCARD_UNCHANGED_HEARTBEAT
        sb.AppendLine("          preprocessing:");
        sb.AppendLine("            - type: JAVASCRIPT");
        sb.AppendLine("              parameters:");
        sb.AppendLine("                - |");
        foreach (var line in rule.JsPreprocessing.Split('\n'))
        {
            sb.AppendLine($"                  {line}");
        }
        sb.AppendLine("            - type: DISCARD_UNCHANGED_HEARTBEAT");
        sb.AppendLine("              parameters:");
        sb.AppendLine("                - 1h");

        sb.AppendLine("          master_item:");
        sb.AppendLine($"            key: {rule.MasterItemKey}");
    }

    private static void WriteItemPrototype(StringBuilder sb, ItemPrototype proto)
    {
        sb.AppendLine($"            - uuid: {proto.Uuid}");
        sb.AppendLine($"              name: '{EscapeSingleQuote(proto.Name)}'");
        sb.AppendLine("              type: DEPENDENT");
        // Keys containing macros need quoting
        if (proto.Key.Contains('{') || proto.Key.Contains('}'))
            sb.AppendLine($"              key: '{EscapeSingleQuote(proto.Key)}'");
        else
            sb.AppendLine($"              key: {proto.Key}");
        sb.AppendLine("              delay: '0'");
        if (!string.IsNullOrEmpty(proto.Units))
            sb.AppendLine($"              units: {proto.Units}");
        if (proto.ValueType == ZabbixValueType.Char)
        {
            sb.AppendLine("              trends: '0'");
            sb.AppendLine("              value_type: CHAR");
        }
        else if (proto.ValueType == ZabbixValueType.Float)
        {
            sb.AppendLine("              value_type: FLOAT");
        }
        else if (proto.ValueType == ZabbixValueType.Text)
        {
            sb.AppendLine("              trends: '0'");
            sb.AppendLine("              value_type: TEXT");
        }
        if (!string.IsNullOrEmpty(proto.Description))
            sb.AppendLine($"              description: '{EscapeSingleQuote(proto.Description)}'");
        sb.AppendLine("              preprocessing:");
        // JsonPath may contain macros — quote it
        sb.AppendLine("                - type: JSONPATH");
        sb.AppendLine("                  parameters:");
        if (proto.JsonPath.Contains('{') || proto.JsonPath.Contains('}'))
            sb.AppendLine($"                    - '{EscapeSingleQuote(proto.JsonPath)}'");
        else
            sb.AppendLine($"                    - {proto.JsonPath}");
        sb.AppendLine("              master_item:");
        sb.AppendLine($"                key: {proto.MasterItemKey}");
        sb.AppendLine("              tags:");
        sb.AppendLine("                - tag: component");
        sb.AppendLine($"                  value: {proto.Component}");
    }

    private static void WriteTriggerPrototype(StringBuilder sb, TriggerPrototype tp, string templateName)
    {
        sb.AppendLine($"            - uuid: {tp.Uuid}");
        sb.AppendLine($"              expression: '{EscapeSingleQuote(tp.Expression)}'");
        sb.AppendLine($"              name: '{EscapeSingleQuote(tp.Name)}'");
        sb.AppendLine($"              priority: {tp.Priority}");
        if (!string.IsNullOrEmpty(tp.RecoveryExpression))
        {
            sb.AppendLine("              recovery_mode: RECOVERY_EXPRESSION");
            sb.AppendLine($"              recovery_expression: '{EscapeSingleQuote(tp.RecoveryExpression)}'");
        }
        if (!string.IsNullOrEmpty(tp.Description))
            sb.AppendLine($"              description: '{EscapeSingleQuote(tp.Description)}'");
        if (tp.Tags.Count > 0)
        {
            sb.AppendLine("              tags:");
            foreach (var (tag, value) in tp.Tags)
            {
                sb.AppendLine($"                - tag: {tag}");
                sb.AppendLine($"                  value: {value}");
            }
        }
    }

    private static void WriteGraph(StringBuilder sb, Graph graph, string templateName)
    {
        var graphTypeStr = graph.Type switch
        {
            GraphType.StackedLine => "STACKED",
            GraphType.PieChart => "PIE",
            _ => "NORMAL"
        };
        sb.AppendLine($"    - uuid: {graph.Uuid}");
        sb.AppendLine($"      name: '{EscapeSingleQuote(graph.Name)}'");
        sb.AppendLine($"      type: {graphTypeStr}");
        sb.AppendLine("      graph_items:");
        foreach (var item in graph.Items)
        {
            sb.AppendLine($"        - color: {item.Color}");
            sb.AppendLine("          item:");
            sb.AppendLine($"            host: '{EscapeSingleQuote(templateName)}'");
            sb.AppendLine($"            key: '{EscapeSingleQuote(item.ItemKey)}'");
        }
    }

    private static void WriteTrigger(StringBuilder sb, Trigger trigger, string indent)
    {
        sb.AppendLine($"{indent}- uuid: {trigger.Uuid}");
        sb.AppendLine($"{indent}  expression: '{EscapeSingleQuote(trigger.Expression)}'");
        sb.AppendLine($"{indent}  name: '{EscapeSingleQuote(trigger.Name)}'");
        sb.AppendLine($"{indent}  priority: {trigger.Priority}");
        if (!string.IsNullOrEmpty(trigger.RecoveryExpression))
        {
            sb.AppendLine($"{indent}  recovery_mode: RECOVERY_EXPRESSION");
            sb.AppendLine($"{indent}  recovery_expression: '{EscapeSingleQuote(trigger.RecoveryExpression)}'");
        }
        if (!string.IsNullOrEmpty(trigger.Description))
            sb.AppendLine($"{indent}  description: '{EscapeSingleQuote(trigger.Description)}'");
        if (trigger.Tags.Count > 0)
        {
            sb.AppendLine($"{indent}  tags:");
            foreach (var (tag, value) in trigger.Tags)
            {
                sb.AppendLine($"{indent}    - tag: {tag}");
                sb.AppendLine($"{indent}      value: {value}");
            }
        }
    }

    private static string EscapeSingleQuote(string value)
        => value.Replace("'", "''");
}
