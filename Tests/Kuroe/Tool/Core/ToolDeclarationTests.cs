using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具声明：定义承载路径、说明、参数与调用体，路径的末段是函数名。</summary>
public sealed class ToolDeclarationTests
{
    [Fact]
    public void ToolDefinition_exposes_its_declaration()
    {
        ToolDefinition definition = new(new ToolPath("GetTime"), "取时间", []);
        ToolFunction function = definition.Bind(_ => "现在");

        Assert.Equal(new ToolPath("GetTime"), function.Definition.Path);
        Assert.Equal(new ToolName("GetTime"), function.Definition.Path.Name);
        Assert.Equal("取时间", function.Definition.Description);
        Assert.Empty(function.Definition.Parameters);
        Assert.Equal("现在", function.Invoke(new ToolArguments(new Dictionary<string, object?>())).Value);
    }

    [Fact]
    public void ToolDefinition_path_is_the_declared_identity()
    {
        ToolDefinition grouped = new(new ToolPath("file/ReadFile"), "读文件", []);

        ToolDefinition regrouped = grouped with { Path = new ToolPath("source/ReadFile") };

        Assert.Equal(new ToolName("ReadFile"), grouped.Path.Name);
        Assert.Equal(new ToolPath("source/ReadFile"), regrouped.Path);
    }
}
