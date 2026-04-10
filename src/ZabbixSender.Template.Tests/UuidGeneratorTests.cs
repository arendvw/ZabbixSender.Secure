using NUnit.Framework;
using Shouldly;

namespace ZabbixSender.Template.Tests;

[TestFixture]
public class UuidGeneratorTests
{
    [Test]
    public void Generate_ReturnsThirtyTwoHexChars()
    {
        var uuid = UuidGenerator.Generate("SampleApp by Zabbix trapper", "sampleapp.data");
        uuid.Length.ShouldBe(32);
        uuid.ShouldMatch("^[0-9a-f]{32}$");
    }

    [Test]
    public void Generate_IsDeterministic()
    {
        var uuid1 = UuidGenerator.Generate("MyTemplate", "myapp.data");
        var uuid2 = UuidGenerator.Generate("MyTemplate", "myapp.data");
        uuid1.ShouldBe(uuid2);
    }

    [Test]
    public void Generate_DifferentInputsProduceDifferentUuids()
    {
        var uuid1 = UuidGenerator.Generate("MyTemplate", "myapp.data");
        var uuid2 = UuidGenerator.Generate("MyTemplate", "myapp.memory.free");
        uuid1.ShouldNotBe(uuid2);
    }
}
