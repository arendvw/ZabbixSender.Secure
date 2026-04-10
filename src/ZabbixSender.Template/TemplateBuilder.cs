using System.Reflection;
using ZabbixSender.Template.Model;

namespace ZabbixSender.Template;

public class TemplateBuilder<TPayload> where TPayload : class
{
    private readonly string _name;
    private readonly string _prefix;
    private readonly string _templateName;
    private readonly List<Type> _types = [];
    private readonly List<(Type ValueType, string Collection, string? MacroName)> _discoveries = [];
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
        _prefix = _name.ToLowerInvariant().Replace(" ", "");
        _templateName = $"{_name} by Zabbix trapper";
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

    public TemplateBuilder<TPayload> Add<T>() where T : class
    {
        _types.Add(typeof(T));
        return this;
    }

    public TemplateBuilder<TPayload> AddDiscovery<T>(string collection, string? macroName = null) where T : class
    {
        _discoveries.Add((typeof(T), collection.ToLowerInvariant(), macroName));
        return this;
    }

    /// <summary>
    /// Add a discovery rule by finding the Dictionary&lt;string, TValue&gt; property on TParent.
    /// The property name becomes the collection name and macro.
    /// </summary>
    public TemplateBuilder<TPayload> AddDiscovery<TParent, TValue>() where TValue : class
    {
        var prop = typeof(TParent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
                p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && p.PropertyType.GetGenericArguments()[1] == typeof(TValue));

        if (prop == null)
            throw new ArgumentException(
                $"{typeof(TParent).Name} has no Dictionary<string, {typeof(TValue).Name}> property");

        _discoveries.Add((typeof(TValue), prop.Name.ToLowerInvariant(), null));
        return this;
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

        var allDependentItems = new List<DependentItem>();
        var allDiscoveryRules = new List<DiscoveryRule>();

        var triggers = new List<Trigger>();

        // Process registered types
        foreach (var type in _types)
        {
            var section = DeriveSection(type);
            var prefixAttr = type.GetCustomAttribute<ZabbixPrefixAttribute>();
            var typeReflector = prefixAttr != null
                ? new TypeReflector(prefixAttr.Prefix, _templateName, _name)
                : reflector;
            ProcessType(typeReflector, type, section, allDependentItems, allDiscoveryRules, triggers);
        }

        // Process explicit discoveries
        foreach (var (valueType, collection, macroName) in _discoveries)
        {
            allDiscoveryRules.Add(reflector.ReflectDiscovery(valueType, collection, macroName));
        }
        if (!_nodataDisabled)
        {
            var masterKey = $"{_prefix}.data";
            var triggerExpression = $"nodata(/{_templateName}/{masterKey},{_nodataSeconds})";
            triggers.Add(new Trigger
            {
                Uuid = UuidGenerator.Generate(_templateName, $"{_prefix}.nodata.trigger"),
                Name = $"{_name}: No data received",
                Expression = triggerExpression,
                Priority = "HIGH",
                Tags = [("scope", "availability")]
            });
        }

        // Build graphs
        var graphs = GenerateGraphs(allDependentItems);

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
            DependentItems = allDependentItems,
            DiscoveryRules = allDiscoveryRules,
            Triggers = triggers,
            Graphs = graphs
        };
    }

    private void ProcessType(
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
                var collection = prop.Name.ToLowerInvariant();
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
            _sectionTypes[section] = type;
        }
    }

    private List<Graph> GenerateGraphs(List<DependentItem> dependentItems)
    {
        var graphs = new List<Graph>();

        // Group items by the originating type for graph-attribute checks
        // We need to look at the types to find [PieChart] / [StackedGraph] / [SingleGraph] attributes
        // Group dependent items by component (section)
        var byComponent = dependentItems
            .GroupBy(i => i.Component)
            .ToList();

        foreach (var group in byComponent)
        {
            var component = group.Key;
            var items = group.ToList();

            // Find the type that produced this component
            _sectionTypes.TryGetValue(component, out var sourceType);

            // Determine numeric items (not Char)
            var numericItems = items
                .Where(i => i.ValueType != ZabbixValueType.Char)
                .ToList();

            if (numericItems.Count == 0)
                continue;

            // Determine graph type override from attributes
            GraphType? overrideType = null;
            if (sourceType != null)
            {
                if (sourceType.GetCustomAttribute<StackedGraphAttribute>() != null)
                    overrideType = GraphType.StackedLine;
                else if (sourceType.GetCustomAttribute<SingleGraphAttribute>() != null)
                    overrideType = GraphType.SingleLine;
            }

            // Determine default graph type: multiple items with same unit → stacked, otherwise single line
            bool allSameUnit = numericItems.Count > 1 &&
                numericItems.Select(i => i.Units ?? "").Distinct().Count() == 1;
            GraphType defaultType = allSameUnit ? GraphType.StackedLine : GraphType.SingleLine;
            GraphType graphType = overrideType ?? defaultType;

            var graphName = $"{_name}: {TypeReflector.FormatSection(component)}";
            var graphUuid = UuidGenerator.Generate(_templateName, $"{_prefix}.{component}.graph");

            var graph = new Graph
            {
                Uuid = graphUuid,
                Name = graphName,
                Type = graphType,
                Items = numericItems
                    .Select((item, index) => new GraphItem
                    {
                        ItemKey = item.Key,
                        Color = GraphColors[index % GraphColors.Length]
                    })
                    .ToList()
            };
            graphs.Add(graph);

            // If [PieChart] attribute present, also add a pie chart
            if (sourceType != null)
            {
                var pieAttr = sourceType.GetCustomAttribute<PieChartAttribute>();
                if (pieAttr != null)
                {
                    var pieUuid = UuidGenerator.Generate(_templateName, $"{_prefix}.{component}.piechart");
                    var pie = new Graph
                    {
                        Uuid = pieUuid,
                        Name = pieAttr.Title,
                        Type = GraphType.PieChart,
                        Items = numericItems
                            .Select((item, index) => new GraphItem
                            {
                                ItemKey = item.Key,
                                Color = GraphColors[index % GraphColors.Length]
                            })
                            .ToList()
                    };
                    graphs.Add(pie);
                }
            }
        }

        return graphs;
    }

    private static string DeriveSection(Type type)
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
        return name.ToLowerInvariant();
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
