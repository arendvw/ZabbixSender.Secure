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
        yaml.ShouldContain("units: MB");

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
}
