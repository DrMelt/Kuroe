using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具声明：定义承载路径、说明、参数与调用体，路径的末段是函数名。</summary>
public sealed class ToolDefinitionTests
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
    public void Parameter_groups_declare_the_exclusive_choices()
    {
        ToolParameter project = new(new ToolName("project"), "项目", Path: true);
        ToolParameter solution = new(new ToolName("solution"), "解决方案", Path: true);
        ToolDefinition definition = new(
            new ToolPath("Test"),
            "测试",
            [project, solution],
            Groups: [new ToolParameterGroup([project.Name, solution.Name])]);

        ToolParameterGroup group = Assert.Single(definition.ParameterGroups);

        Assert.Equal([project.Name, solution.Name], group.Parameters);
        Assert.Null(group.InvalidReason(definition.Parameters));
    }

    [Fact]
    public void Bind_rejects_groups_with_duplicates_or_undeclared_parameters()
    {
        ToolParameter project = new(new ToolName("project"), "项目");

        Assert.Contains(
            "含重复参数 project。",
            Assert.Throws<InvalidOperationException>(() => new ToolDefinition(
                    new ToolPath("Test"),
                    "测试",
                    [project],
                    Groups: [new ToolParameterGroup([project.Name, project.Name])])
                .Bind(_ => "结果")).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "含未声明的参数 solution。",
            Assert.Throws<InvalidOperationException>(() => new ToolDefinition(
                    new ToolPath("Test"),
                    "测试",
                    [project],
                    Groups: [new ToolParameterGroup([new ToolName("solution")])])
                .Bind(_ => "结果")).Message,
            StringComparison.Ordinal);
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
