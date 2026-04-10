using NUnit.Framework;
using Shouldly;
using ZabbixSender.Template.Tests.Fixtures;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class YamlSerializerTests
{
    [Test]
    public void ToYaml_ContainsZabbixExportHeader()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("zabbix_export:");
        yaml.ShouldContain("version: '7.0'");
    }

    [Test]
    public void ToYaml_ContainsTemplateGroup()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("template_groups:");
        yaml.ShouldContain("name: Templates/Applications");
    }

    [Test]
    public void ToYaml_ContainsMasterItem()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("type: TRAP");
        yaml.ShouldContain("key: test.data");
        yaml.ShouldContain("value_type: TEXT");
    }

    [Test]
    public void ToYaml_ContainsDependentItem()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("type: DEPENDENT");
        yaml.ShouldContain("key: test.users.signedin");
        yaml.ShouldContain("type: JSONPATH");
    }

    [Test]
    public void ToYaml_CharValueType_SetsTrendsZero()
    {
        var template = new TemplateBuilder("Test").AddDiscovery<HealthCheckStatus>("Health").Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("value_type: CHAR");
        yaml.ShouldContain("trends: '0'");
    }

    [Test]
    public void ToYaml_ContainsNodataTrigger()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("nodata(");
        yaml.ShouldContain("priority: HIGH");
    }

    [Test]
    public void ToYaml_DiscoveryRule_ContainsJsPreprocessing()
    {
        var template = new TemplateBuilder("Test").AddDiscovery<HealthCheckStatus>("Health").Build();
        var yaml = YamlSerializer.ToYaml(template);
        yaml.ShouldContain("discovery_rules:");
        yaml.ShouldContain("type: JAVASCRIPT");
        yaml.ShouldContain("DISCARD_UNCHANGED_HEARTBEAT");
        yaml.ShouldContain("lld_macro_paths:");
    }

    [Test]
    public void ToYaml_UnsignedValueType_OmitsValueType()
    {
        var template = new TemplateBuilder("Test").Add<UsersMetrics>().Build();
        var yaml = YamlSerializer.ToYaml(template);
        // Check the dependent item for SignedIn doesn't have value_type
        var lines = yaml.Split('\n');
        var inItem = false;
        var foundValueType = false;
        foreach (var line in lines)
        {
            if (line.Contains("key: test.users.signedin")) inItem = true;
            if (inItem && line.Contains("type: JSONPATH")) break;
            if (inItem && line.Contains("value_type:")) foundValueType = true;
        }
        foundValueType.ShouldBeFalse("UNSIGNED items should omit value_type");
    }
}
