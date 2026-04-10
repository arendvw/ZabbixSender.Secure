using System.Text.RegularExpressions;
using NUnit.Framework;
using Shouldly;
using ZabbixSender.Template.Tests.Fixtures;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class IntegrationTests
{
    [Test]
    public void FullTemplate_GeneratesValidYaml()
    {
        var template = new TemplateBuilder<SampleAppPayload>().Build();
        var yaml = template.ToYaml();

        // structure
        yaml.ShouldContain("zabbix_export:");
        yaml.ShouldContain("version: '7.0'");
        yaml.ShouldContain("template: 'SampleApp by Zabbix trapper'");

        // master item
        yaml.ShouldContain("key: sampleapp.data");
        yaml.ShouldContain("type: TRAP");

        // items from payload properties
        yaml.ShouldContain("key: sampleapp.memory.app");
        yaml.ShouldContain("key: sampleapp.memory.postgres");
        yaml.ShouldContain("key: sampleapp.memory.free");
        yaml.ShouldContain("units: B");
        yaml.ShouldContain("key: sampleapp.users.signedin");

        // discovery from Dictionary property
        yaml.ShouldContain("key: sampleapp.health.discovery");
        yaml.ShouldContain("{#HEALTH}");
        yaml.ShouldContain("type: JAVASCRIPT");

        // triggers
        yaml.ShouldContain("nodata(");
        yaml.ShouldContain("Degraded");
        yaml.ShouldContain("Unhealthy");

        // UUIDs valid and unique
        var uuidPattern = new Regex(@"uuid: ([0-9a-f]{32})");
        var matches = uuidPattern.Matches(yaml);
        matches.Count.ShouldBeGreaterThan(5);
        var uuids = matches.Select(m => m.Groups[1].Value).ToList();
        uuids.Distinct().Count().ShouldBe(uuids.Count);
    }

    [Test]
    public void FullTemplate_IsDeterministic()
    {
        var yaml1 = new TemplateBuilder<SampleAppPayload>().Build().ToYaml();
        var yaml2 = new TemplateBuilder<SampleAppPayload>().Build().ToYaml();
        yaml1.ShouldBe(yaml2);
    }

    [Test]
    public void FullPayload_GeneratesImportableTemplate()
    {
        var template = new TemplateBuilder<TestAppPayload>().Build();
        var yaml = template.ToYaml();

        // structure
        yaml.ShouldContain("zabbix_export:");
        yaml.ShouldContain("version: '7.0'");
        yaml.ShouldContain("template: 'TestApp by Zabbix trapper'");

        // master item
        yaml.ShouldContain("key: testapp.data");
        yaml.ShouldContain("type: TRAP");

        // items (section = property name on TestAppPayload, camelCased)
        yaml.ShouldContain("key: testapp.status.uptime");
        yaml.ShouldContain("key: testapp.clients.workers");
        yaml.ShouldContain("key: testapp.memory.app");
        yaml.ShouldContain("key: testapp.disk.available");
        yaml.ShouldContain("key: testapp.database.activeconnections");
        yaml.ShouldContain("key: testapp.jobs.failedpermanently");
        yaml.ShouldContain("key: testapp.http.requests");

        // units
        yaml.ShouldContain("units: uptime");
        yaml.ShouldContain("units: B");

        // discovery
        yaml.ShouldContain("discovery_rules:");
        yaml.ShouldContain("type: JAVASCRIPT");

        // triggers
        yaml.ShouldContain("priority: WARNING");
        yaml.ShouldContain("priority: HIGH");
        yaml.ShouldContain("priority: INFO");
        yaml.ShouldContain("nodata(");

        // recovery expressions
        yaml.ShouldContain("recovery_mode: RECOVERY_EXPRESSION");
        yaml.ShouldContain("recovery_expression:");

        // change per second preprocessing
        yaml.ShouldContain("CHANGE_PER_SECOND");

        // pie chart
        yaml.ShouldContain("type: PIE");

        // UUIDs valid
        var uuidPattern = new Regex(@"uuid: ([0-9a-f]{32})");
        var matches = uuidPattern.Matches(yaml);
        matches.Count.ShouldBeGreaterThan(15);

        // write to disk for manual zabbix import testing
        var outputPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "testapp-template.yaml");
        File.WriteAllText(outputPath, yaml);
        TestContext.WriteLine($"Template written to: {outputPath}");
        TestContext.WriteLine($"  {template.DependentItems.Count} dependent items");
        TestContext.WriteLine($"  {template.DiscoveryRules.Count} discovery rules");
        TestContext.WriteLine($"  {template.Triggers.Count} triggers");
        TestContext.WriteLine($"  {template.Graphs.Count} graphs");
    }
}
