using System.Reflection;
using System.Text;
using ZabbixSender.Template.Model;

namespace ZabbixSender.Template;

public class TypeReflector(string prefix, string templateName, string? displayName = null)
{
    private string DisplayName => displayName ?? FormatPrefix(prefix);

    public List<DependentItem> ReflectItems(Type type, string section)
    {
        var items = new List<DependentItem>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<ZabbixIgnoreAttribute>() != null)
                continue;

            if (!IsLeafType(prop.PropertyType))
                continue;

            var keyOverride = prop.GetCustomAttribute<ItemKeyAttribute>();
            var key = keyOverride?.Key ?? $"{prefix}.{section}.{prop.Name.ToLowerInvariant()}";
            var jsonPath = $"$.{section}.{ToCamelCase(prop.Name)}";
            var units = prop.GetCustomAttribute<UnitsAttribute>()?.Units;
            var description = prop.GetCustomAttribute<ZabbixDescriptionAttribute>()?.Description;
            var valueType = MapValueType(prop.PropertyType);

            var extra = new List<PreprocessingStep>();
            if (prop.GetCustomAttribute<ChangePerSecondAttribute>() != null)
                extra.Add(new PreprocessingStep { Type = "CHANGE_PER_SECOND" });

            items.Add(new DependentItem
            {
                Uuid = UuidGenerator.Generate(templateName, key),
                Name = $"{DisplayName}: {FormatSection(section)} - {FormatPropertyName(prop.Name)}",
                Key = key,
                JsonPath = jsonPath,
                MasterItemKey = $"{prefix}.data",
                ValueType = valueType,
                Units = units,
                Description = description,
                Component = section,
                ExtraPreprocessing = extra
            });
        }

        return items;
    }

    public DiscoveryRule ReflectDiscovery(Type valueType, string collection, string? macroName)
    {
        var resolvedMacro = macroName ?? collection.ToUpperInvariant();
        var macroRef = $"{{#{resolvedMacro}}}";
        var discoveryKey = $"{prefix}.{collection}.discovery";

        var prototypes = new List<ItemPrototype>();
        var triggerPrototypes = new List<TriggerPrototype>();

        foreach (var prop in valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<ZabbixIgnoreAttribute>() != null)
                continue;

            var propNameLower = prop.Name.ToLowerInvariant();
            var propNameCamel = ToCamelCase(prop.Name);
            var protoKey = $"{prefix}.{collection}.{propNameLower}[{macroRef}]";
            var jsonPath = $"$.{collection}.{macroRef}.{propNameCamel}";
            var valueTypeMapped = MapValueType(prop.PropertyType);

            prototypes.Add(new ItemPrototype
            {
                Uuid = UuidGenerator.Generate(templateName, protoKey),
                Name = $"{DisplayName}: {FormatSection(collection)} - {macroRef} {prop.Name.ToLowerInvariant()}",
                Key = protoKey,
                JsonPath = jsonPath,
                MasterItemKey = $"{prefix}.data",
                ValueType = valueTypeMapped,
                Units = prop.GetCustomAttribute<UnitsAttribute>()?.Units,
                Description = prop.GetCustomAttribute<ZabbixDescriptionAttribute>()?.Description,
                Component = collection
            });

            foreach (var triggerAttr in prop.GetCustomAttributes<TriggerAttribute>())
            {
                var expression = BuildTriggerExpression(triggerAttr, templateName, protoKey, valueTypeMapped);
                var recovery = BuildRecoveryExpression(triggerAttr, templateName, protoKey, valueTypeMapped);
                var triggerLabel = triggerAttr.Value ?? $"{triggerAttr.Operator}{triggerAttr.Threshold}";

                triggerPrototypes.Add(new TriggerPrototype
                {
                    Uuid = UuidGenerator.Generate(templateName, $"{protoKey}.trigger.{triggerLabel}"),
                    Expression = expression,
                    RecoveryExpression = recovery,
                    Name = $"{DisplayName}: {FormatSection(collection)} {macroRef} {prop.Name.ToLowerInvariant()} is {{ITEM.LASTVALUE1}}",
                    Priority = triggerAttr.Priority.ToString().ToUpperInvariant(),
                    Tags = [("scope", "availability")]
                });
            }

            foreach (var changeAttr in prop.GetCustomAttributes<TriggerOnChangeAttribute>())
            {
                var keyRef = $"/{templateName}/{protoKey}";
                var expression = changeAttr.Duration != null
                    ? $"change({keyRef})<>0 or diff({keyRef})=1"
                    : $"change({keyRef})<>0 or diff({keyRef})=1";
                var recovery = changeAttr.Duration != null
                    ? $"nodata({keyRef},{changeAttr.Duration})=0 and change({keyRef})=0"
                    : $"change({keyRef})=0 and diff({keyRef})=0";

                triggerPrototypes.Add(new TriggerPrototype
                {
                    Uuid = UuidGenerator.Generate(templateName, $"{protoKey}.trigger.change"),
                    Expression = expression,
                    RecoveryExpression = recovery,
                    Name = $"{DisplayName}: {FormatSection(collection)} {macroRef} {prop.Name.ToLowerInvariant()} changed",
                    Priority = changeAttr.Priority.ToString().ToUpperInvariant(),
                    Tags = [("scope", "notice")]
                });
            }
        }

        var jsPreprocessing = $"var data = JSON.parse(value);\n" +
            $"var result = [];\n" +
            $"if (data.{collection}) {{\n" +
            $"  Object.keys(data.{collection}).forEach(function(key) {{\n" +
            $"    result.push({{\"name\": key}});\n" +
            $"  }});\n" +
            $"}}\n" +
            $"return JSON.stringify(result);";

        return new DiscoveryRule
        {
            Uuid = UuidGenerator.Generate(templateName, discoveryKey),
            Name = $"{DisplayName}: {FormatSection(collection)} discovery",
            Key = discoveryKey,
            MasterItemKey = $"{prefix}.data",
            MacroName = macroRef,
            MacroPath = "$.name",
            JsPreprocessing = jsPreprocessing,
            CollectionJsonProperty = collection,
            ItemPrototypes = prototypes,
            TriggerPrototypes = triggerPrototypes
        };
    }

    public static ZabbixValueType MapValueType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
            return ZabbixValueType.Float;
        if (underlying == typeof(string))
            return ZabbixValueType.Char;
        return ZabbixValueType.Unsigned;
    }

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static bool IsLeafType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying == typeof(string) || underlying == typeof(decimal);
    }

    internal static string FormatPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return prefix;
        return char.ToUpperInvariant(prefix[0]) + prefix[1..];
    }

    internal static string FormatSection(string section)
    {
        if (string.IsNullOrEmpty(section)) return section;
        return char.ToUpperInvariant(section[0]) + section[1..];
    }

    public static string BuildTriggerExpression(
        TriggerAttribute trigger, string templateName, string itemKey, ZabbixValueType valueType)
    {
        var keyRef = $"/{templateName}/{itemKey}";

        // exact string match: last(/template/key)="value"
        if (trigger.Value != null)
        {
            var func = trigger.Duration != null
                ? $"min({keyRef},{trigger.Duration})"
                : $"last({keyRef})";

            return valueType == ZabbixValueType.Char
                ? $"{func}=\"{trigger.Value}\""
                : $"{func}={trigger.Value}";
        }

        // numeric comparison: last(/template/key)>threshold
        // with duration, pick the right aggregation function:
        //   > or >= : use min (the minimum over the period must exceed threshold)
        //   < or <= : use max (the maximum over the period must be below threshold)
        //   = or <> : use last (duration doesn't change the function for equality)
        if (trigger.Threshold != null)
        {
            string func;
            if (trigger.Duration != null)
            {
                func = trigger.Operator switch
                {
                    ">" or ">=" => $"min({keyRef},{trigger.Duration})",
                    "<" or "<=" => $"max({keyRef},{trigger.Duration})",
                    _ => $"last({keyRef})"
                };
            }
            else
            {
                func = $"last({keyRef})";
            }

            return $"{func}{trigger.Operator}{trigger.Threshold}";
        }

        return $"last({keyRef}){trigger.Operator}0";
    }

    public static string? BuildRecoveryExpression(
        TriggerAttribute trigger, string templateName, string itemKey, ZabbixValueType valueType)
    {
        if (trigger.Recovery == null)
            return null;

        var keyRef = $"/{templateName}/{itemKey}";

        // for string match: recover when value no longer equals the trigger value
        if (trigger.Value != null)
        {
            return valueType == ZabbixValueType.Char
                ? $"last({keyRef})<>\"{trigger.Value}\""
                : $"last({keyRef})<>{trigger.Value}";
        }

        // for numeric: invert the operator over the recovery period
        if (trigger.Threshold != null)
        {
            var inverseOp = trigger.Operator switch
            {
                ">" => "<=",
                ">=" => "<",
                "<" => ">=",
                "<=" => ">",
                _ => "<>"
            };
            var func = $"last({keyRef})";
            return $"{func}{inverseOp}{trigger.Threshold}";
        }

        return null;
    }

    private static string FormatPropertyName(string name)
    {
        var result = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result.Append(' ');
                result.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                result.Append(name[i]);
            }
        }
        return result.ToString();
    }
}
