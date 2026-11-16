using Kuroe.Assistant.Dialogue;
using Kuroe.Assistant.Dialogue.Flow;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Assistant.Tests.Dialogue;

/// <summary>内置对话的代码定义：回话参数、模式工具白名单与路径变量。</summary>
public sealed class DialogueFlowTests
{
    private const string DefaultLanguage = "中文";

    [Fact]
    public void Dialogue_parameters_carry_the_identity_mode_and_language_system_prompt()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Read, DefaultLanguage);

        Assert.Equal(
            [
                "你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。",
                "当前为只读模式：只读取工作区与查询状态，不修改文件。",
                "使用「中文」语言母语者语言风格回答。",
            ],
            parameters.SystemPrompt);
        Assert.Null(parameters.Prompt);
    }

    [Fact]
    public void Dialogue_parameters_use_the_configured_language()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Edit, "English");

        Assert.Equal(
            [
                "你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。",
                "当前为更改模式：可以修改工作区文件。仓库改动与构建、测试、格式化命令不在当前模式的能力范围。",
                "使用「English」语言母语者语言风格回答。",
            ],
            parameters.SystemPrompt);
    }

    [Fact]
    public void Dialogue_flow_whitelists_read_tools_by_path()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Read, DefaultLanguage);

        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("KuroeInfo"),
                new ToolPath("file/read"),
                new ToolPath("source"),
                new ToolPath("git/read"),
                new ToolPath("dotnet/read"),
            ],
            parameters.Tools);
    }

    [Fact]
    public void Dialogue_flow_whitelists_edit_tools_by_path()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Edit, DefaultLanguage);

        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("KuroeInfo"),
                new ToolPath("file/read"),
                new ToolPath("file/write"),
                new ToolPath("source"),
                new ToolPath("git/read"),
                new ToolPath("dotnet/read"),
            ],
            parameters.Tools);
    }

    [Fact]
    public void Edit_mode_does_not_grant_repository_or_command_write_tools()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Edit, DefaultLanguage);

        Assert.DoesNotContain(parameters.Tools, tool => tool.Value == "git/write");
        Assert.DoesNotContain(parameters.Tools, tool => tool.Value == "dotnet/write");
        Assert.DoesNotContain(parameters.Tools, tool => tool.Value == "git/branch");
    }

    [Fact]
    public void Dialogue_parameters_map_the_workspace_port_to_tool_roots()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DialogueMode.Read, DefaultLanguage);

        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["file"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["git"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["dotnet"][PathVariables.RootName]);
    }

    [Fact]
    public void Dialogue_mode_parse_falls_back_to_read()
    {
        Assert.Same(DialogueMode.Edit, DialogueMode.Parse("edit"));
        Assert.Same(DialogueMode.Edit, DialogueMode.Parse("EDIT"));
        Assert.Same(DialogueMode.Read, DialogueMode.Parse("unknown"));
        Assert.Same(DialogueMode.Read, DialogueMode.Parse(null));
    }

    [Fact]
    public void Dialogue_reply_step_is_a_single_root_path()
    {
        Assert.Equal("回话", DialogueFlow.ReplyStep.Value);
    }
}