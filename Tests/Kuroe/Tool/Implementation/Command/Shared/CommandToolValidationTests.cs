using Kuroe.Shared;
using Xunit;

namespace Kuroe.Tool.Command.Shared.Tests;

/// <summary>命令定义的模板一致性规则。</summary>
public sealed class CommandToolValidationTests
{
    [Fact]
    public void Template_reasons_accept_a_consistent_definition()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [
                new ToolParameter(new ToolName("files"), "列表", Required: true, List: true),
                new ToolParameter(new ToolName("staged"), "开关", Flag: true),
                new ToolParameter(new ToolName("revision"), "修订", Required: true),
            ],
            [
                new("tool", null),
                new("--staged", "staged"),
                new("--files={files}", null),
                new("{revision}", null),
            ],
            Variables: [PathVariables.RootName]);

        Assert.Empty(CommandToolValidation.TemplateReasons(definition));
    }

    [Fact]
    public void Template_reasons_accept_multiple_list_placeholders()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [
                new ToolParameter(new ToolName("files"), "列表", List: true),
                new ToolParameter(new ToolName("others"), "列表", List: true),
            ],
            [new("tool", null), new("{files}:{others}", null)]);

        Assert.Empty(CommandToolValidation.TemplateReasons(definition));
    }

    [Fact]
    public void Template_reasons_reject_unlinked_flag()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [new ToolParameter(new ToolName("staged"), "开关", Flag: true)],
            [new("tool", null)]);

        Assert.Contains(
            CommandToolValidation.TemplateReasons(definition),
            reason => reason.Contains("没有被任何模板项关联", StringComparison.Ordinal));
    }

    [Fact]
    public void Template_reasons_reject_variable_and_parameter_with_same_name()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [new ToolParameter(new ToolName("root"), "路径", Path: true)],
            [new("tool", null), new("{root}", null)],
            Variables: [PathVariables.RootName]);

        Assert.Contains(
            CommandToolValidation.TemplateReasons(definition),
            reason => reason.Contains("与参数同名", StringComparison.Ordinal));
    }

    [Fact]
    public void Ensure_valid_throws_for_an_inconsistent_definition()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [new ToolParameter(new ToolName("files"), "列表", List: true)],
            [new("tool", null)]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => CommandToolValidation.EnsureValid([definition]));

        Assert.Contains("参数 files 已声明但没有出现在模板里", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_reasons_accept_variable_prefixed_item()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [],
            [new("{root}/src", null)],
            Variables: [PathVariables.RootName]);

        Assert.Empty(CommandToolValidation.TemplateReasons(definition));
    }

    [Fact]
    public void Template_reasons_allow_root_variable_without_template_use()
    {
        CommandToolDefinition definition = new(
            new ToolPath("Test"),
            "说明",
            [],
            [new("tool", null)],
            Variables: [PathVariables.RootName]);

        Assert.Empty(CommandToolValidation.TemplateReasons(definition));
    }
}