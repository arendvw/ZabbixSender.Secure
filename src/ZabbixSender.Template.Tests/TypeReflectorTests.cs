using NUnit.Framework;
using Shouldly;
using ZabbixSender.Template.Model;
using ZabbixSender.Template.Tests.Fixtures;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class TypeReflectorTests
{
    [Test]
    public void ReflectItems_LeafProperties_CreatesDependentItems()
    {
        var reflector = new TypeReflector("sampleapp", "SampleApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(MemoryMetrics), "memory");

        result.Count.ShouldBe(3);
        result[0].Key.ShouldBe("sampleapp.memory.app");
        result[0].JsonPath.ShouldBe("$.memory.app");
        result[0].Units.ShouldBe("B");
        result[0].Component.ShouldBe("memory");
        result[0].ValueType.ShouldBe(ZabbixValueType.Unsigned);
    }

    [Test]
    public void ReflectItems_StringProperty_SetsCharValueType()
    {
        var reflector = new TypeReflector("myapp", "MyApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(StringFixture), "test");
        result[0].ValueType.ShouldBe(ZabbixValueType.Char);
    }

    [Test]
    public void ReflectItems_DoubleProperty_SetsFloatValueType()
    {
        var reflector = new TypeReflector("myapp", "MyApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(FloatFixture), "test");
        result[0].ValueType.ShouldBe(ZabbixValueType.Float);
    }

    [Test]
    public void ReflectItems_IgnoredProperty_IsSkipped()
    {
        var reflector = new TypeReflector("myapp", "MyApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(IgnoredFixture), "test");
        result.Count.ShouldBe(1);
        result[0].Key.ShouldBe("myapp.test.visible");
    }

    [Test]
    public void ReflectItems_ItemKeyOverride_UsesCustomKey()
    {
        var reflector = new TypeReflector("myapp", "MyApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(CustomKeyFixture), "test");
        result[0].Key.ShouldBe("myapp.custom.key");
    }

    [Test]
    public void ReflectItems_NullableInt_SameAsInt()
    {
        var reflector = new TypeReflector("myapp", "MyApp by Zabbix trapper");
        var result = reflector.ReflectItems(typeof(NullableFixture), "test");
        result[0].ValueType.ShouldBe(ZabbixValueType.Unsigned);
    }

    [Test]
    public void ReflectDiscovery_CreatesDiscoveryRuleWithPrototypes()
    {
        var reflector = new TypeReflector("sampleapp", "SampleApp by Zabbix trapper");
        var rule = reflector.ReflectDiscovery(typeof(HealthCheckStatus), "health", macroName: null);

        rule.Key.ShouldBe("sampleapp.health.discovery");
        rule.MacroName.ShouldBe("{#HEALTH}");
        rule.ItemPrototypes.Count.ShouldBe(2);
        rule.ItemPrototypes[0].Key.ShouldBe("sampleapp.health.status[{#HEALTH}]");
        rule.ItemPrototypes[0].JsonPath.ShouldBe("$.health.{#HEALTH}.status");
        rule.ItemPrototypes[1].Key.ShouldBe("sampleapp.health.description[{#HEALTH}]");
    }

    [Test]
    public void ReflectDiscovery_CustomMacro_OverridesDefault()
    {
        var reflector = new TypeReflector("sampleapp", "SampleApp by Zabbix trapper");
        var rule = reflector.ReflectDiscovery(typeof(HealthCheckStatus), "health", macroName: "HEALTHCHECK");

        rule.MacroName.ShouldBe("{#HEALTHCHECK}");
        rule.ItemPrototypes[0].Key.ShouldBe("sampleapp.health.status[{#HEALTHCHECK}]");
    }

    [Test]
    public void ReflectDiscovery_TriggerAttributes_CreatesTriggerPrototypes()
    {
        var reflector = new TypeReflector("sampleapp", "SampleApp by Zabbix trapper");
        var rule = reflector.ReflectDiscovery(typeof(HealthCheckStatus), "health", macroName: null);

        rule.TriggerPrototypes.Count.ShouldBe(2);
        rule.TriggerPrototypes[0].Priority.ShouldBe("WARNING");
        rule.TriggerPrototypes[0].Expression.ShouldContain("=\"Degraded\"");
        rule.TriggerPrototypes[1].Priority.ShouldBe("HIGH");
    }

    [Test]
    public void ReflectDiscovery_JsPreprocessing_ExtractsKeys()
    {
        var reflector = new TypeReflector("sampleapp", "SampleApp by Zabbix trapper");
        var rule = reflector.ReflectDiscovery(typeof(HealthCheckStatus), "health", macroName: null);

        rule.JsPreprocessing.ShouldContain("data.health");
        rule.JsPreprocessing.ShouldContain("JSON.stringify(result)");
    }

    private class StringFixture { public string Name { get; set; } = ""; }
    private class FloatFixture { public double Value { get; set; } }
    private class IgnoredFixture
    {
        public int Visible { get; set; }
        [ZabbixIgnore] public int Hidden { get; set; }
    }
    private class CustomKeyFixture
    {
        [ItemKey("myapp.custom.key")] public int Value { get; set; }
    }
    private class NullableFixture { public int? Count { get; set; } }

    // trigger expression tests

    [Test]
    public void BuildTriggerExpression_ExactStringMatch()
    {
        var trigger = new TriggerAttribute("Degraded", Priority.Warning);
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Char);
        expr.ShouldBe("last(/T/k)=\"Degraded\"");
    }

    [Test]
    public void BuildTriggerExpression_NumericGreaterThan()
    {
        var trigger = new TriggerAttribute(">", 90, Priority.High);
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Unsigned);
        expr.ShouldBe("last(/T/k)>90");
    }

    [Test]
    public void BuildTriggerExpression_NumericLessThan()
    {
        var trigger = new TriggerAttribute("<", 10, Priority.Warning);
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Float);
        expr.ShouldBe("last(/T/k)<10");
    }

    [Test]
    public void BuildTriggerExpression_NumericWithDuration_GreaterThan_UsesMin()
    {
        var trigger = new TriggerAttribute(">", 90, Priority.High) { Duration = "5m" };
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Unsigned);
        expr.ShouldBe("min(/T/k,5m)>90");
    }

    [Test]
    public void BuildTriggerExpression_NumericWithDuration_LessThan_UsesMax()
    {
        var trigger = new TriggerAttribute("<", 10, Priority.Warning) { Duration = "1h" };
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Unsigned);
        expr.ShouldBe("max(/T/k,1h)<10");
    }

    [Test]
    public void BuildTriggerExpression_StringMatchWithDuration_UsesMin()
    {
        var trigger = new TriggerAttribute("Degraded", Priority.Warning) { Duration = "5m" };
        var expr = TypeReflector.BuildTriggerExpression(trigger, "T", "k", ZabbixValueType.Char);
        expr.ShouldBe("min(/T/k,5m)=\"Degraded\"");
    }

    [Test]
    public void BuildRecoveryExpression_NoRecovery_ReturnsNull()
    {
        var trigger = new TriggerAttribute("Degraded", Priority.Warning);
        var expr = TypeReflector.BuildRecoveryExpression(trigger, "T", "k", ZabbixValueType.Char);
        expr.ShouldBeNull();
    }

    [Test]
    public void BuildRecoveryExpression_StringMatch_InvertsToNotEqual()
    {
        var trigger = new TriggerAttribute("Degraded", Priority.Warning) { Recovery = "5m" };
        var expr = TypeReflector.BuildRecoveryExpression(trigger, "T", "k", ZabbixValueType.Char);
        expr.ShouldBe("last(/T/k)<>\"Degraded\"");
    }

    [Test]
    public void BuildRecoveryExpression_GreaterThan_InvertsToLessOrEqual()
    {
        var trigger = new TriggerAttribute(">", 90, Priority.High) { Recovery = "5m" };
        var expr = TypeReflector.BuildRecoveryExpression(trigger, "T", "k", ZabbixValueType.Unsigned);
        expr.ShouldBe("last(/T/k)<=90");
    }

    [Test]
    public void BuildRecoveryExpression_LessThan_InvertsToGreaterOrEqual()
    {
        var trigger = new TriggerAttribute("<", 10, Priority.Warning) { Recovery = "3m" };
        var expr = TypeReflector.BuildRecoveryExpression(trigger, "T", "k", ZabbixValueType.Unsigned);
        expr.ShouldBe("last(/T/k)>=10");
    }

    [Test]
    public void ReflectDiscovery_TriggerOnChange_CreatesChangePrototype()
    {
        var reflector = new TypeReflector("app", "App by Zabbix trapper");
        var rule = reflector.ReflectDiscovery(typeof(ChangeFixture), "versions", macroName: null);

        rule.TriggerPrototypes.Count.ShouldBe(1);
        rule.TriggerPrototypes[0].Expression.ShouldContain("change(");
        rule.TriggerPrototypes[0].RecoveryExpression.ShouldNotBeNull();
        rule.TriggerPrototypes[0].Name.ShouldContain("changed");
        rule.TriggerPrototypes[0].Priority.ShouldBe("INFO");
    }

    private class ChangeFixture
    {
        [TriggerOnChange(Priority.Info, Duration = "5m")]
        public string Version { get; set; } = "";
    }
}
