using System.Reflection;
using Kuroe.Configuration;
using Kuroe.Shared.Configuration;
using Xunit;

namespace Kuroe.Tests;

/// <summary>偏好路径、声明表与用户层形状的一致性。</summary>
public sealed class SettingsTests
{
    /// <summary>列出的节与项来自声明表，路径与设置项同源。</summary>
    [Fact]
    public void Listing_comes_from_the_declared_items()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        SettingSection section = Assert.Single(environment.Settings.Sections);

        Assert.Equal("Runtime", section.Name);
        Assert.Contains(section.Entries, entry => entry.Path == "Runtime:Temperature");
        Assert.All(section.Entries, entry => Assert.Equal("Runtime", entry.Path.Split(':')[0]));
    }

    /// <summary>声明表、设置节的属性与用户层的形状三者一一对应，任一处漏写时失败。</summary>
    [Fact]
    public void Declarations_the_section_and_the_file_shape_agree()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Assembly library = typeof(SettingsProvider).Assembly;

        IEnumerable<string> section = Properties(library, "Kuroe.Configuration.Model.RuntimeSettings");
        IEnumerable<string> shape = Properties(library, "Kuroe.Configuration.Model.RuntimeSectionDto");
        IEnumerable<string> declared = environment.Settings.Sections.Single().Entries
            .Select(entry => entry.Name)
            .Order();

        Assert.Equal(section, declared);
        Assert.Equal(section, shape);
    }

    /// <summary>用户层里缺的项取声明的默认值，不被写成空值。</summary>
    [Fact]
    public void Absent_items_keep_their_declared_defaults()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        IReadOnlyList<SettingEntry> entries = environment.Settings.Sections.Single().Entries;

        Assert.Equal("未设置", Value(entries, "Temperature"));
        Assert.Equal("Warning", Value(entries, "LogLevel"));
        Assert.Equal("4", Value(entries, "MaxConcurrentExecutions"));
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
        Assert.Equal("Runtime:Temperature", SettingsProvider.ResolvePath("runtime:temperature").ThrowIfError());
        Assert.Contains("不是已定义的设置项", SettingsProvider.ResolvePath("Runtime:Nope").FirstError.Description);
    }
}
