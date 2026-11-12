using Kuroe.Assistant.Dialogue;
using Kuroe.Assistant.Flows;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Assistant.Tests;

/// <summary>内置对话的代码定义：回话参数、工具白名单与路径变量。</summary>
public sealed class DialogueTests
{
    private const string DefaultLanguage = "中文";

    [Fact]
    public void Dialogue_parameters_carry_the_identity_and_language_system_prompt()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DefaultLanguage);

        Assert.Equal(
            ["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。", "使用「中文」语言母语者语言风格回答。"],
            parameters.SystemPrompt);
        Assert.Null(parameters.Prompt);
    }

    [Fact]
    public void Dialogue_parameters_use_the_configured_language()
    {
        LlmParameters parameters = DialogueFlow.Parameters("English");

        Assert.Equal(
            ["你是Kuroe的内置助手，直接回应用户的输入。回复内容写在正常输出里。", "使用「English」语言母语者语言风格回答。"],
            parameters.SystemPrompt);
    }

    [Fact]
    public void Dialogue_flow_whitelists_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("KuroeInfo"),
                new ToolPath("file/read"),
                new ToolPath("source"),
                new ToolPath("git/read"),
                new ToolPath("dotnet/read"),
            ],
            DialogueFlow.Tools);
    }

    [Fact]
    public void Dialogue_parameters_map_the_workspace_port_to_tool_roots()
    {
        LlmParameters parameters = DialogueFlow.Parameters(DefaultLanguage);

        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["file"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["git"][PathVariables.RootName]);
        Assert.Equal(DialogueFlow.WorkspacePort.Value, parameters.ToolPorts["dotnet"][PathVariables.RootName]);
    }

    [Fact]
    public void Dialogue_reply_step_is_a_single_root_path()
    {
        Assert.Equal("回话", DialogueFlowControl.ReplyStep.Value);
    }
}