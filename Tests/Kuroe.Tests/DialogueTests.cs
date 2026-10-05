using Kuroe.Executions.Tools;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Tools;
using Kuroe.Tools.KuroeTools;
using Kuroe.Workflows.Tasks;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Tests;

/// <summary>前台对话的固定节点配置：节点名、产出契约与工具白名单的应用。</summary>
public sealed class DialogueTests
{
    [Fact]
    public void Defaults_declare_dialogue_node_with_text_output()
    {
        Assert.Equal(NodeName.Dialogue, DialogueDefaults.Name);
        Assert.Equal(NodeOutput.Text, DialogueDefaults.Output);
    }

    [Fact]
    public void Defaults_whitelist_declares_dialogue_tools_by_path()
    {
        Assert.Equal(
            [
                new ToolPath("GetLocalTime"),
                new ToolPath("info"),
            ],
            DialogueDefaults.Tools);
    }

    [Fact]
    public void Tool_face_filters_to_whitelist()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
            new SampleTool(),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), DialogueDefaults.Tools);

        Assert.Equal(
            ["GetLocalTime", "ListTasks", "GetTask", "GetRun", "GetActiveTask"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_parent_path_grants_all_descendants()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
            new SampleTool(),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("info")]);

        Assert.Equal(
            ["ListTasks", "GetTask", "GetRun", "GetActiveTask"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_full_path_grants_only_that_function()
    {
        ToolCollection tools = new(
        [
            new TimeTool(),
            new TaskInfoTool(new TaskRegistry()),
        ]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("info/GetTask")]);

        Assert.Equal(["GetTask"], built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_with_similar_prefix_does_not_match_group()
    {
        ToolCollection tools = new([new TaskInfoTool(new TaskRegistry())]);
        TurnScope scope = Scope();

        IReadOnlyList<AITool> built = tools.Build(scope, new NullSink(), [new ToolPath("infoX")]);

        Assert.Empty(built);
    }

    private static TurnScope Scope() => new()
    {
        Task = new TaskId(1),
        Run = new RunId(1),
        Output = DialogueDefaults.Output,
        NodeName = DialogueDefaults.Name,
        ItemIndex = null,
        Journal = new TurnJournal(),
        Sink = new NullSink(),
    };

    private sealed class SampleTool : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
        [
            new ToolFunction(new ToolName("RunDiff"), "样例工具", [], _ => string.Empty),
        ];
    }

    private sealed class NullSink : ITurnSink
    {
        public void OnText(string delta)
        {
        }

        public void OnToolCall(ToolCallRecord record)
        {
        }
    }
}