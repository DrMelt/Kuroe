using Kuroe.Shared.Configuration;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>配置查询工具：用户层文件、各节设置项与来源标记。</summary>
public sealed class SettingsKuroeInfoToolTests
{
    [Fact]
    public void Describe_renders_sections_and_source_marks()
    {
        SettingsKuroeInfoTool tool = new(new FakeSettings());

        string output = ToolResult.Render(Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?>())));

        Assert.Contains("用户层文件：settings.json", output, StringComparison.Ordinal);
        Assert.Contains("运行:", output, StringComparison.Ordinal);
        Assert.Contains("模型 · fake · 用户层已设置", output, StringComparison.Ordinal);
    }

    private sealed class FakeSettings : ISettingsView
    {
        public string UserSettingsFile => "settings.json";

        public IReadOnlyList<SettingSection> Sections =>
        [
            new SettingSection("运行", [new SettingEntry("Runtime.Model", "模型", "fake", true)]),
        ];
    }
}
