using System.Reflection;
using Kuroe.Configuration;
using Kuroe.Shared.Configuration;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>偏好写入的两条通道：按 JSON 字面量解析的值，与命令认定的名称一类文本。</summary>
public sealed class SettingsTests
{
    [Fact]
    public void Literal_write_refuses_a_name_that_looks_like_json()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        Assert.True(harness.Settings.Set("Agent:DefaultFlow", "3").IsError);
    }

    [Fact]
    public void Text_write_accepts_a_name_that_looks_like_json()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        harness.Settings.SetText("Agent:DefaultFlow", "3").ThrowIfError();

        Assert.Equal("3", harness.Flows.DefaultName);
    }

    /// <summary>列出的节与项来自声明表，路径与设置项同源。</summary>
    [Fact]
    public void Listing_comes_from_the_declared_items()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        SettingSection section = Assert.Single(harness.Settings.Sections());

        Assert.Equal("Agent", section.Name);
        Assert.Contains(section.Entries, entry => entry.Path == "Agent:Temperature");
        Assert.All(section.Entries, entry => Assert.Equal("Agent", entry.Path.Split(':')[0]));
    }

    /// <summary>声明表、设置节的属性与用户层的形状三者一一对应，任一处漏写时失败。</summary>
    [Fact]
    public void Declarations_the_section_and_the_file_shape_agree()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        Assembly library = typeof(SettingsProvider).Assembly;

        IEnumerable<string> section = Properties(library, "Kuroe.Configuration.AgentSettings");
        IEnumerable<string> shape = Properties(library, "Kuroe.Configuration.AgentSectionDto");
        IEnumerable<string> declared = harness.Settings.Sections().Single().Entries
            .Select(entry => entry.Name)
            .Order();

        Assert.Equal(section, declared);
        Assert.Equal(section, shape);
    }

    /// <summary>用户层里缺的项取声明的默认值，不被写成空值。</summary>
    [Fact]
    public void Absent_items_keep_their_declared_defaults()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        IReadOnlyList<SettingEntry> entries = harness.Settings.Sections().Single().Entries;

        Assert.Equal("未设置", Value(entries, "DefaultFlow"));
        Assert.NotEqual("未设置", Value(entries, "SystemPrompt"));
        Assert.Equal("Warning", Value(entries, "LogLevel"));
        Assert.Equal("4", Value(entries, "MaxConcurrentRuns"));
        Assert.Equal("3", Value(entries, "MaxAttempts"));
    }

    private static string Value(IReadOnlyList<SettingEntry> entries, string name) =>
        entries.Single(entry => entry.Name == name).Value;

    /// <summary>库内类型与文件形状都是 internal，测试按名字取。</summary>
    private static IEnumerable<string> Properties(Assembly library, string name) => library
        .GetType(name, throwOnError: true)!
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name)
        .Order();

    /// <summary>路径解析只认已定义的设置项，大小写不敏感。</summary>
    [Fact]
    public void Path_resolution_keeps_defined_items_and_rejects_the_rest()
    {
        Assert.Equal("Agent:Temperature", SettingsProvider.ResolvePath("agent:temperature").ThrowIfError());
        Assert.Contains("不是已定义的设置项", SettingsProvider.ResolvePath("Agent:Nope").FirstError.Description);
    }
}
