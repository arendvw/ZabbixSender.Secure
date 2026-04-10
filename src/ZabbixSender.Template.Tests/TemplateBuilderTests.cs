using NUnit.Framework;
using Shouldly;
using ZabbixSender.Template.Model;
using ZabbixSender.Template.Tests.Fixtures;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class TemplateBuilderTests
{
    // Task 7: Core builder
    [Test]
    public void Build_SetsTemplateNameAndPrefix()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        template.TemplateName.ShouldBe("SampleApp by Zabbix trapper");
        template.Prefix.ShouldBe("sampleapp");
    }

    [Test]
    public void Build_CreatesMasterItem()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        template.MasterItem.Key.ShouldBe("sampleapp.data");
        template.MasterItem.Name.ShouldBe("SampleApp: Raw data");
    }

    [Test]
    public void Build_AddType_CreatesDependentItems()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        template.DependentItems.Count.ShouldBe(3);
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.app");
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.postgres");
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.free");
    }

    [Test]
    public void Build_MultipleAddCalls_CombinesItems()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Add<UsersMetrics>().Build();
        template.DependentItems.Count.ShouldBe(4);
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.users.signedin");
    }

    [Test]
    public void Build_AddDiscovery_CreatesDiscoveryRule()
    {
        var template = new TemplateBuilder<SampleAppPayload>().AddDiscovery<HealthCheckStatus>("Health").Build();
        template.DiscoveryRules.Count.ShouldBe(1);
        template.DiscoveryRules[0].Key.ShouldBe("sampleapp.health.discovery");
        template.DiscoveryRules[0].MacroName.ShouldBe("{#HEALTH}");
    }

    [Test]
    public void Build_GeneratesNodataTriggerByDefault()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        template.Triggers.Count.ShouldBe(1);
        template.Triggers[0].Expression.ShouldContain("nodata");
        template.Triggers[0].Expression.ShouldContain("180");
        template.Triggers[0].Priority.ShouldBe("HIGH");
    }

    [Test]
    public void Build_NodataTriggerCustomSeconds()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().NodataTrigger(seconds: 300).Build();
        template.Triggers[0].Expression.ShouldContain("300");
    }

    [Test]
    public void Build_NodataTriggerDisabled()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().NodataTrigger(disable: true).Build();
        template.Triggers.ShouldBeEmpty();
    }

    [Test]
    public void Build_SectionName_DerivedFromClassName()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        template.DependentItems[0].Component.ShouldBe("memory");
    }

    [Test]
    public void Build_AllUuidsAreDeterministic()
    {
        var t1 = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        var t2 = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        t1.TemplateUuid.ShouldBe(t2.TemplateUuid);
        t1.MasterItem.Uuid.ShouldBe(t2.MasterItem.Uuid);
        t1.DependentItems[0].Uuid.ShouldBe(t2.DependentItems[0].Uuid);
    }

    // Task 8: Dictionary auto-discovery
    [Test]
    public void Build_DictionaryProperty_CreatesDiscoveryRule()
    {
        var template = new TemplateBuilder<MyAppPayload>().Add<AppMetrics>().Build();
        template.DiscoveryRules.Count.ShouldBe(1);
        template.DiscoveryRules[0].Key.ShouldBe("myapp.health.discovery");
        template.DiscoveryRules[0].MacroName.ShouldBe("{#HEALTH}");
    }

    [Test]
    public void Build_DictionaryProperty_LeafPropertiesStillWork()
    {
        var template = new TemplateBuilder<MyAppPayload>().Add<AppMetrics>().Build();
        template.DependentItems.ShouldContain(i => i.Key == "myapp.app.signedin");
        template.DependentItems.ShouldNotContain(i => i.Key.Contains("health"));
    }

    [Test]
    public void Build_DictionaryWithDiscoveryAttribute_UsesCustomMacro()
    {
        var template = new TemplateBuilder<MyAppPayload>().Add<CustomMacroFixture>().Build();
        template.DiscoveryRules[0].MacroName.ShouldBe("{#ENDPOINT}");
    }

    // Task 9: Graph generation
    [Test]
    public void Build_MultipleNumericSameUnit_CreatesStackedLineGraph()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<MemoryMetrics>().Build();
        var graph = template.Graphs.FirstOrDefault(g => g.Type == GraphType.StackedLine);
        graph.ShouldNotBeNull();
        graph.Items.Count.ShouldBe(3);
    }

    [Test]
    public void Build_SingleNumericProperty_CreatesSingleLineGraph()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<UsersMetrics>().Build();
        var graph = template.Graphs.FirstOrDefault(g => g.Type == GraphType.SingleLine);
        graph.ShouldNotBeNull();
        graph.Items.Count.ShouldBe(1);
    }

    [Test]
    public void Build_PieChartAttribute_CreatesPieChart()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<PieChartMemoryMetrics>().Build();
        var pie = template.Graphs.FirstOrDefault(g => g.Type == GraphType.PieChart);
        pie.ShouldNotBeNull();
        pie.Name.ShouldBe("Memory Distribution");
        pie.Items.Count.ShouldBe(3);
    }

    [Test]
    public void Build_PieChartAndStackedLine_BothGenerated()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Add<PieChartMemoryMetrics>().Build();
        template.Graphs.Count(g => g.Type == GraphType.StackedLine).ShouldBe(1);
        template.Graphs.Count(g => g.Type == GraphType.PieChart).ShouldBe(1);
    }
}
