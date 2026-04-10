using System.Reflection;
using ZabbixSender.Template.Model;

namespace ZabbixSender.Template;

public class TemplateBuilder<TPayload> where TPayload : class
{
    private readonly string _name;
    private readonly string _prefix;
    private readonly string _templateName;
    private readonly Dictionary<string, Type> _sectionTypes = new();
    private int _nodataSeconds = 180;
    private bool _nodataDisabled;

    private static readonly string[] SuffixesToStrip = ["Payload", "Metrics", "Data", "Info", "Status"];

    private static readonly string[] GraphColors =
    [
        "FF0000", "00AA00", "0000FF", "FF8800", "AA00AA",
        "00AAAA", "888800", "008888", "880088", "444444"
    ];

    public TemplateBuilder()
    {
        _name = DeriveName(typeof(TPayload));
        _prefix = _name.ToLowerInvariant();
        _templateName = $"{_name} by Zabbix trapper";
    }

    public TemplateBuilder<TPayload> NodataTrigger(int seconds = 180, bool disable = false)
    {
        _nodataSeconds = seconds;
        _nodataDisabled = disable;
        return this;
    }

    public ZabbixTemplate Build()
    {
        var reflector = new TypeReflector(_prefix, _templateName, _name);

        var dependentItems = new List<DependentItem>();
        var discoveryRules = new List<DiscoveryRule>();
        var triggers = new List<Trigger>();

        // iterate all properties on TPayload
        foreach (var prop in typeof(TPayload).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<ZabbixIgnoreAttribute>() != null)
                continue;

            var section = TypeReflector.ToCamelCase(prop.Name);

            if (IsDictionaryType(prop.PropertyType, out var dictValueType))
            {
                // Dictionary<string, T> -> discovery rule
                var discoveryAttr = prop.GetCustomAttribute<DiscoveryAttribute>();
                var macroName = discoveryAttr?.MacroName;
                discoveryRules.Add(reflector.ReflectDiscovery(dictValueType!, section, macroName));
            }
            else if (TypeReflector.IsLeafType(prop.PropertyType))
            {
                // leaf property directly on payload (unusual but supported)
                dependentItems.AddRange(reflector.ReflectItems(typeof(TPayload), section));
                triggers.AddRange(reflector.ReflectTriggers(typeof(TPayload), section));
            }
            else
            {
                // nested class -> process its properties as items
                var nestedType = prop.PropertyType;
                ProcessNestedType(reflector, nestedType, section, dependentItems, discoveryRules, triggers);
                _sectionTypes[section] = nestedType;
            }
        }

        // nodata trigger
        if (!_nodataDisabled)
        {
            var masterKey = $"{_prefix}.data";
            triggers.Add(new Trigger
            {
                Uuid = UuidGenerator.Generate(_templateName, $"{_prefix}.nodata.trigger"),
                Name = $"{_name}: No data received",
                Expression = $"nodata(/{_templateName}/{masterKey},{_nodataSeconds})",
                Priority = "HIGH",
                Tags = [("scope", "availability")]
            });
        }

        var graphs = GenerateGraphs(dependentItems);
        var masterItemKey = $"{_prefix}.data";

        return new ZabbixTemplate
        {
            TemplateName = _templateName,
            Prefix = _prefix,
            TemplateGroupUuid = UuidGenerator.Generate(_templateName, $"{_prefix}.group"),
            TemplateUuid = UuidGenerator.Generate(_templateName, _prefix),
            MasterItem = new MasterItem
            {
                Uuid = UuidGenerator.Generate(_templateName, masterItemKey),
                Name = $"{_name}: Raw data",
                Key = masterItemKey
            },
            DependentItems = dependentItems,
            DiscoveryRules = discoveryRules,
            Triggers = triggers,
            Graphs = graphs
        };
    }

    private void ProcessNestedType(
        TypeReflector reflector,
        Type type,
        string section,
        List<DependentItem> dependentItems,
        List<DiscoveryRule> discoveryRules,
        List<Trigger> triggers)
    {
        bool hasLeafProperties = false;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<ZabbixIgnoreAttribute>() != null)
                continue;

            if (IsDictionaryType(prop.PropertyType, out var valueType))
            {
                var discoveryAttr = prop.GetCustomAttribute<DiscoveryAttribute>();
                var macroName = discoveryAttr?.MacroName;
                var collection = TypeReflector.ToCamelCase(prop.Name);
                discoveryRules.Add(reflector.ReflectDiscovery(valueType!, collection, macroName));
            }
            else if (TypeReflector.IsLeafType(prop.PropertyType))
            {
                hasLeafProperties = true;
            }
        }

        if (hasLeafProperties)
        {
            dependentItems.AddRange(reflector.ReflectItems(type, section));
            triggers.AddRange(reflector.ReflectTriggers(type, section));
        }
    }

    private List<Graph> GenerateGraphs(List<DependentItem> dependentItems)
    {
        var graphs = new List<Graph>();

        var byComponent = dependentItems
            .GroupBy(i => i.Component)
            .ToList();

        foreach (var group in byComponent)
        {
            var component = group.Key;
            _sectionTypes.TryGetValue(component, out var sourceType);

            var numericItems = group
                .Where(i => i.ValueType != ZabbixValueType.Char)
                .ToList();

            if (numericItems.Count == 0)
                continue;

            GraphType? overrideType = null;
            if (sourceType != null)
            {
                if (sourceType.GetCustomAttribute<StackedGraphAttribute>() != null)
                    overrideType = GraphType.StackedLine;
                else if (sourceType.GetCustomAttribute<SingleGraphAttribute>() != null)
                    overrideType = GraphType.SingleLine;
            }

            bool allSameUnit = numericItems.Count > 1 &&
                numericItems.Select(i => i.Units ?? "").Distinct().Count() == 1;
            GraphType defaultType = allSameUnit ? GraphType.StackedLine : GraphType.SingleLine;
            GraphType graphType = overrideType ?? defaultType;

            graphs.Add(new Graph
            {
                Uuid = UuidGenerator.Generate(_templateName, $"{_prefix}.{component}.graph"),
                Name = $"{_name}: {TypeReflector.FormatSection(component)}",
                Type = graphType,
                Items = numericItems
                    .Select((item, index) => new GraphItem
                    {
                        ItemKey = item.Key,
                        Color = GraphColors[index % GraphColors.Length]
                    })
                    .ToList()
            });

            if (sourceType != null)
            {
                var pieAttr = sourceType.GetCustomAttribute<PieChartAttribute>();
                if (pieAttr != null)
                {
                    graphs.Add(new Graph
                    {
                        Uuid = UuidGenerator.Generate(_templateName, $"{_prefix}.{component}.piechart"),
                        Name = pieAttr.Title,
                        Type = GraphType.PieChart,
                        Items = numericItems
                            .Select((item, index) => new GraphItem
                            {
                                ItemKey = item.Key,
                                Color = GraphColors[index % GraphColors.Length]
                            })
                            .ToList()
                    });
                }
            }
        }

        return graphs;
    }

    private static string DeriveName(Type type)
    {
        var name = type.Name;
        foreach (var suffix in SuffixesToStrip)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        return name;
    }

    private static bool IsDictionaryType(Type type, out Type? valueType)
    {
        valueType = null;
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        if (genericDef != typeof(Dictionary<,>))
            return false;

        var args = type.GetGenericArguments();
        if (args[0] != typeof(string))
            return false;

        valueType = args[1];
        return true;
    }
}
