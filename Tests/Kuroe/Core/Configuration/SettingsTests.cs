using System.Reflection;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Shared.Configuration;
using Kuroe.Tests.Support;
using Xunit;

namespace Kuroe.Tests.Configuration;

/// <summary>偏好路径、声明表与用户层形状的一致性。</summary>
public sealed class SettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly SettingsProvider _settings;

    public SettingsTests()
    {
        KuroePaths paths = KuroePaths.At(_root);
        Directory.CreateDirectory(paths.ConfigDirectory);
        _settings = SettingsProvider.Create(paths).ThrowIfError();
    }
    /// <summary>列出的节与项来自声明表，路径与设置项同源。</summary>
    [Fact]
    public void Listing_comes_from_the_declared_items()
    {
        IReadOnlyList<SettingSection> sections = _settings.Sections;

        Assert.Equal(["Runtime", "Assistant"], sections.Select(section => section.Name));
        Assert.Contains(Entries(sections, "Runtime"), entry => entry.Path == "Runtime:LogLevel");
        Assert.Contains(Entries(sections, "Assistant"), entry => entry.Path == "Assistant:Language");
        Assert.All(sections, section =>
            Assert.All(section.Entries, entry => Assert.Equal(section.Name, entry.Path.Split(':')[0])));
    }

    /// <summary>声明表、设置节的属性与用户层的形状三者一一对应，任一处漏写时失败。</summary>
    [Fact]
    public void Declarations_the_section_and_the_file_shape_agree()
    {
        Assembly library = typeof(SettingsProvider).Assembly;

        foreach (SettingSection section in _settings.Sections)
        {
            IEnumerable<string> settings = Properties(library, $"Kuroe.Configuration.Model.{section.Name}Settings");
            IEnumerable<string> shape = Properties(library, $"Kuroe.Configuration.Model.{section.Name}SectionDto");
            IEnumerable<string> declared = section.Entries.Select(entry => entry.Name).Order();

            Assert.Equal(settings, declared);
            Assert.Equal(settings, shape);
        }
    }

    /// <summary>用户层里缺的项取声明的默认值，不被写成空值。</summary>
    [Fact]
    public void Absent_items_keep_their_declared_defaults()
    {
        IReadOnlyList<SettingEntry> runtime = Entries(_settings.Sections, "Runtime");
        IReadOnlyList<SettingEntry> assistant = Entries(_settings.Sections, "Assistant");

        Assert.Equal("Warning", Value(runtime, "LogLevel"));
        Assert.Equal("中文", Value(assistant, "Language"));
    }

    private static IReadOnlyList<SettingEntry> Entries(IReadOnlyList<SettingSection> sections, string name) =>
        sections.Single(section => section.Name == name).Entries;

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
        Assert.Equal("Runtime:LogLevel", SettingsProvider.ResolvePath("runtime:loglevel").ThrowIfError());
        Assert.Equal("Assistant:Language", SettingsProvider.ResolvePath("assistant:language").ThrowIfError());
        Assert.Contains("不是已定义的设置项", SettingsProvider.ResolvePath("Runtime:Nope").FirstError.Description);
    }

    [Fact]
    public void Language_change_requires_dialogue_rebuild()
    {
        SettingsEffect language = _settings.Set("Assistant:Language", "English").ThrowIfError();

        Assert.True(language.RequiresDialogueRebuild);
        Assert.False(language.RequiresRestart);
        Assert.False(language.InvalidatesHistory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}
