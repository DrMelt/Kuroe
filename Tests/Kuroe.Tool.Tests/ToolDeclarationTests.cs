using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具声明：定义承载的元数据与调用体，路径由名字与分组派生。</summary>
public sealed class ToolDeclarationTests
{
    [Fact]
    public void ToolDefinition_exposes_its_declaration()
    {
        ToolDefinition definition = new(new ToolName("GetTime"), "取时间", []);
        ToolFunction function = definition.Bind(_ => "现在");

        Assert.Equal(new ToolName("GetTime"), function.Definition.Name);
        Assert.Equal("取时间", function.Definition.Description);
        Assert.Empty(function.Definition.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())).Value);
    }

    [Fact]
    public void ToolDefinition_path_is_built_from_name_and_group()
    {
        ToolDefinition plain = new(new ToolName("GetTime"), "取时间", []);
        ToolDefinition grouped = new(new ToolName("ReadFile"), "读文件", [], new ToolPath("files"));
        ToolDefinition blank = new(new ToolName("GetTime"), "取时间", [], new ToolPath(string.Empty));

        Assert.Equal(new ToolPath("GetTime"), plain.Path);
        Assert.Equal(new ToolPath("files/ReadFile"), grouped.Path);
        Assert.Equal(new ToolPath("GetTime"), blank.Path);
    }

    [Fact]
    public void ToolDefinition_path_follows_derived_values()
    {
        ToolDefinition grouped = new(new ToolName("ReadFile"), "读文件", [], new ToolPath("files"));

        ToolDefinition renamed = grouped with { Name = new ToolName("WriteFile") };
        ToolDefinition regrouped = grouped with { Group = new ToolPath("source") };

        Assert.Equal(new ToolPath("files/WriteFile"), renamed.Path);
        Assert.Equal(new ToolPath("source/ReadFile"), regrouped.Path);
    }
}
