using NUnit.Framework;
using Shouldly;
using ZabbixSender.Template.Model;
using ZabbixSender.Template.Tests.Fixtures;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class TemplateBuilderTests
{
    [Test]
    public void Build_SetsTemplateNameAndPrefix()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        template.TemplateName.ShouldBe("SampleApp by Zabbix trapper");
        template.Prefix.ShouldBe("sampleapp");
    }

    [Test]
    public void Build_CreatesMasterItem()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        template.MasterItem.Key.ShouldBe("sampleapp.data");
        template.MasterItem.Name.ShouldBe("SampleApp: Raw data");
    }

    [Test]
    public void Build_CreatesDependentItemsFromPayload()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        // Memory has 3 properties, Users has 1
        template.DependentItems.Count.ShouldBe(4);
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.app");
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.postgres");
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.memory.free");
        template.DependentItems.ShouldContain(i => i.Key == "sampleapp.users.signedin");
    }

    [Test]
    public void Build_DictionaryProperty_CreatesDiscoveryRule()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        template.DiscoveryRules.Count.ShouldBe(1);
        template.DiscoveryRules[0].Key.ShouldBe("sampleapp.health.discovery");
        template.DiscoveryRules[0].MacroName.ShouldBe("{#HEALTH}");
    }

    [Test]
    public void Build_GeneratesNodataTriggerByDefault()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        template.Triggers.ShouldContain(t => t.Expression.Contains("nodata"));
        template.Triggers.ShouldContain(t => t.Priority == "HIGH");
    }

    [Test]
    public void Build_NodataTriggerCustomSeconds()
    {
        var template = new TemplateBuilder<SampleAppPayload>().NodataTrigger(seconds: 300).Build();
        template.Triggers.ShouldContain(t => t.Expression.Contains("300"));
    }

    [Test]
    public void Build_NodataTriggerDisabled()
    {
        var template = new TemplateBuilder<SampleAppPayload>().NodataTrigger(disable: true).Build();
        template.Triggers.ShouldNotContain(t => t.Expression.Contains("nodata"));
    }

    [Test]
    public void Build_SectionName_DerivedFromPropertyName()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        template.DependentItems.ShouldContain(i => i.Component == "memory");
        template.DependentItems.ShouldContain(i => i.Component == "users");
    }

    [Test]
    public void Build_AllUuidsAreDeterministic()
    {
        var t1 = new TemplateBuilder<SampleAppPayload>().Build();
        var t2 = new TemplateBuilder<SampleAppPayload>().Build();
        t1.TemplateUuid.ShouldBe(t2.TemplateUuid);
        t1.MasterItem.Uuid.ShouldBe(t2.MasterItem.Uuid);
        t1.DependentItems[0].Uuid.ShouldBe(t2.DependentItems[0].Uuid);
    }

    [Test]
    public void Build_StackedLineGraph_ForSameUnitItems()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        var graph = template.Graphs.FirstOrDefault(g => g.Type == GraphType.StackedLine);
        graph.ShouldNotBeNull();
        graph.Items.Count.ShouldBe(3); // memory has 3 items with same unit
    }

    [Test]
    public void Build_SingleLineGraph_ForSingleItem()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        var graph = template.Graphs.FirstOrDefault(g => g.Type == GraphType.SingleLine);
        graph.ShouldNotBeNull();
        graph.Items.Count.ShouldBe(1); // users has 1 item
    }

    [Test]
    public void Build_PieChart_WhenAttributePresent()
    {
        // MemoryMetrics has [PieChart("Memory distribution")]
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        var pie = template.Graphs.FirstOrDefault(g => g.Type == GraphType.PieChart);
        pie.ShouldNotBeNull();
        pie.Name.ShouldBe("Memory distribution");
    }
}
