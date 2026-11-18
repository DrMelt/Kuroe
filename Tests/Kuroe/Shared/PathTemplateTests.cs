using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>路径模板与路径变量规则的纯函数行为。</summary>
public sealed class PathTemplateTests
{
    [Fact(DisplayName = "占位符保持顺序并去重")]
    public void Placeholders_keep_order_and_remove_duplicates()
    {
        Assert.Equal(["root", "name"], PathTemplate.Placeholders("{root}/a/{name}/{root}"));
    }

    [Fact(DisplayName = "展开替换已知变量并保留未知变量")]
    public void Expand_replaces_known_variables_and_keeps_unknown_ones()
    {
        string expanded = PathTemplate.Expand("{root}/{missing}/file", new Dictionary<string, string>
        {
            ["root"] = "C:/workspace",
        });

        Assert.Equal("C:/workspace/{missing}/file", expanded);
    }

    [Fact(DisplayName = "非法名称原因拒绝空白与花括号")]
    public void Invalid_name_reason_rejects_blank_and_braces()
    {
        Assert.Equal("路径变量名不能为空。", PathTemplate.InvalidNameReason(" ", "路径"));
        Assert.Equal("命令变量名 a b 不能含空白与花括号。", PathTemplate.InvalidNameReason("a b", "命令"));
        Assert.NotNull(PathTemplate.InvalidNameReason("a{b}", "路径"));
        Assert.Null(PathTemplate.InvalidNameReason("root", "路径"));
    }

    [Fact(DisplayName = "路径变量报告缺失未使用与非文本端口")]
    public void Path_variables_report_missing_unused_and_non_text_ports()
    {
        IReadOnlyList<string> errors = PathVariables.Validate(
            new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["extra"] = "项目路径",
            },
            ["root", "project"],
            port => port switch
            {
                "任务路径" => true,
                "项目路径" => false,
                _ => null,
            });

        Assert.Contains(errors, error => error.Contains("项目路径 必须是 Text", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("路径占位符 {project} 没有对应的路径变量映射", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("路径变量 extra 没有被 Path 引用", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "映射完整时路径变量通过")]
    public void Path_variables_pass_when_the_mapping_is_complete()
    {
        IReadOnlyList<string> errors = PathVariables.Validate(
            new Dictionary<string, string> { ["root"] = "任务路径" },
            ["root"],
            port => port == "任务路径" ? true : null);

        Assert.Empty(errors);
    }

    [Fact(DisplayName = "展开解析模板并拒绝未映射或越界路径")]
    public void Expand_resolves_a_template_and_rejects_unmapped_or_escaping_paths()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-expand"));
        Dictionary<string, string> variables = new() { ["root"] = root };

        Assert.Equal(
            Path.Combine(root, "out", "file.md"),
            PathVariables.Expand("{root}/out/file.md", variables, root, root, "Path").Value);

        Assert.Contains(
            "含未映射的路径变量",
            PathVariables.Expand("{root}/{missing}", variables, root, root, "Path").FirstError.Description,
            StringComparison.Ordinal);

        Assert.Contains(
            "不是工作区内的合法路径",
            PathVariables.Expand("{root}/../escape.md", variables, root, root, "Path").FirstError.Description,
            StringComparison.Ordinal);
    }
}