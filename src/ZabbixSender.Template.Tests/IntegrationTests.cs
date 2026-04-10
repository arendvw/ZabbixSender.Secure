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
        var template = new TemplateBuilder("SampleApp")
            .Add<MemoryMetrics>()
            .Add<UsersMetrics>()
            .AddDiscovery<HealthCheckStatus>("Health")
            .Build();

        var yaml = template.ToYaml();

        // Structure
        yaml.ShouldContain("zabbix_export:");
        yaml.ShouldContain("version: '7.0'");
        yaml.ShouldContain("template: 'SampleApp by Zabbix trapper'");

        // Master item
        yaml.ShouldContain("key: sampleapp.data");
        yaml.ShouldContain("type: TRAP");

        // Memory items
        yaml.ShouldContain("key: sampleapp.memory.app");
        yaml.ShouldContain("key: sampleapp.memory.postgres");
        yaml.ShouldContain("key: sampleapp.memory.free");
        yaml.ShouldContain("units: B");

        // Users
        yaml.ShouldContain("key: sampleapp.users.signedin");

        // Discovery
        yaml.ShouldContain("key: sampleapp.health.discovery");
        yaml.ShouldContain("{#HEALTH}");
        yaml.ShouldContain("type: JAVASCRIPT");

        // Triggers
        yaml.ShouldContain("nodata(");
        yaml.ShouldContain("Degraded");
        yaml.ShouldContain("Unhealthy");
        yaml.ShouldContain("priority: WARNING");
        yaml.ShouldContain("priority: HIGH");

        // UUIDs are 32 hex chars
        var uuidPattern = new Regex(@"uuid: ([0-9a-f]{32})");
        var matches = uuidPattern.Matches(yaml);
        matches.Count.ShouldBeGreaterThan(5);

        // All UUIDs should be unique
        var uuids = matches.Select(m => m.Groups[1].Value).ToList();
        uuids.Distinct().Count().ShouldBe(uuids.Count);
    }

    [Test]
    public void FullTemplate_IsDeterministic()
    {
        var yaml1 = new TemplateBuilder("SampleApp")
            .Add<MemoryMetrics>().Add<UsersMetrics>()
            .AddDiscovery<HealthCheckStatus>("Health")
            .Build().ToYaml();

        var yaml2 = new TemplateBuilder("SampleApp")
            .Add<MemoryMetrics>().Add<UsersMetrics>()
            .AddDiscovery<HealthCheckStatus>("Health")
            .Build().ToYaml();

        yaml1.ShouldBe(yaml2);
    }

    [Test]
    public void FullPayload_GeneratesImportableTemplate()
    {
        var template = new TemplateBuilder("TestApp")
            .Add<AppStatus>()
            .Add<ConnectedClients>()
            .Add<MemoryMetrics>()
            .Add<DiskStatus>()
            .Add<DatabaseStatus>()
            .Add<JobStatus>()
            .Add<HttpMetrics>()
            .AddDiscovery<AppStatus, HealthEntry>()
            .Build();

        var yaml = template.ToYaml();

        // structure
        yaml.ShouldContain("zabbix_export:");
        yaml.ShouldContain("version: '7.0'");
        yaml.ShouldContain("template: 'TestApp by Zabbix trapper'");

        // master item
        yaml.ShouldContain("key: testapp.data");
        yaml.ShouldContain("type: TRAP");

        // dependent items (section = class name with suffix stripped)
        // section = class name lowercased, with Metrics/Data/Info/Status suffix stripped
        yaml.ShouldContain("key: testapp.app.uptime");              // AppStatus -> app
        yaml.ShouldContain("key: testapp.connectedclients.workers"); // ConnectedClients -> connectedclients
        yaml.ShouldContain("key: testapp.memory.app");              // MemoryMetrics -> memory
        yaml.ShouldContain("key: testapp.disk.available");          // DiskStatus -> disk
        yaml.ShouldContain("key: testapp.database.activeconnections"); // DatabaseStatus -> database
        yaml.ShouldContain("key: testapp.job.failedpermanently");   // JobStatus -> job
        yaml.ShouldContain("key: testapp.http.requests");           // HttpMetrics -> http

        // units
        yaml.ShouldContain("units: uptime");
        yaml.ShouldContain("units: B");

        // discovery rule
        yaml.ShouldContain("discovery_rules:");
        yaml.ShouldContain("key: testapp.healthstatus.discovery");
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

        // pie chart (MemoryMetrics has [PieChart])
        yaml.ShouldContain("type: PIE");

        // UUIDs are valid 32-char hex
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
