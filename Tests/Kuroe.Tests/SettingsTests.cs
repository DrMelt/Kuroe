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
}