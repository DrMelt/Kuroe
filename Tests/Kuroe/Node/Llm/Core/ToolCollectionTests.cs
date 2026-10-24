using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>工具面的白名单：按路径给出函数，父路径覆盖子树，模型侧名字不含路径分隔符。</summary>
public sealed class ToolCollectionTests
{
    [Fact]
    public void Tool_face_filters_to_whitelist()
    {
        ToolCollection tools = new(
        [
            new FakeTool("GetLocalTime"),
            new FakeTool("KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"),
            new FakeTool("file/read/ReadFile"),
        ],
        new ToolJson());

        IReadOnlyList<AITool> built = tools.Build(
            new ExecutionId(1), NoSink, new ProcessJournal(),
            [new ToolPath("GetLocalTime"), new ToolPath("KuroeInfo"), new ToolPath("file")]);

        Assert.Equal(
            ["GetLocalTime", "KuroeInfo_ListTasks", "KuroeInfo_GetTask", "KuroeInfo_GetExecution", "file_read_ReadFile"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_parent_path_grants_all_descendants()
    {
        ToolCollection tools = new(
        [
            new FakeTool("GetLocalTime"),
            new FakeTool("KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"),
        ],
        new ToolJson());

        IReadOnlyList<AITool> built = tools.Build(
            new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfo")]);

        Assert.Equal(
            ["KuroeInfo_ListTasks", "KuroeInfo_GetTask", "KuroeInfo_GetExecution"],
            built.Select(tool => tool.Name));
    }

    [Fact]
    public void Whitelist_full_path_grants_only_that_function()
    {
        ToolCollection tools = new(
        [
            new FakeTool("KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"),
        ],
        new ToolJson());

        IReadOnlyList<AITool> built = tools.Build(
            new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfo/GetTask")]);

        Assert.Equal(["KuroeInfo_GetTask"], built.Select(tool => tool.Name));
    }

    /// <summary>模型接口只接受字母、数字、下划线与连字符，路径含层级时交给模型的名字必须已扁平化。</summary>
    [Fact]
    public void Model_facing_names_fit_the_model_interface_character_set()
    {
        ToolCollection tools = new(
        [
            new FakeTool("KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"),
            new FakeTool("file/read/ReadFile"),
        ],
        new ToolJson());

        IReadOnlyList<AITool> built = tools.Build(
            new ExecutionId(1), NoSink, new ProcessJournal(),
            [new ToolPath("KuroeInfo"), new ToolPath("file")]);

        Assert.Equal(
            ["KuroeInfo_ListTasks", "KuroeInfo_GetTask", "KuroeInfo_GetExecution", "file_read_ReadFile"],
            built.Select(tool => tool.Name));
        Assert.All(built, tool => Assert.Matches("^[a-zA-Z0-9_-]+$", tool.Name));
    }

    [Fact]
    public void Definitions_keep_full_paths_in_declaration_order()
    {
        ToolCollection tools = new(
            [new FakeTool("GetLocalTime", "KuroeInfo/ListTasks", "file/read/ReadFile")],
            new ToolJson());

        Assert.Equal(
            ["GetLocalTime", "KuroeInfo/ListTasks", "file/read/ReadFile"],
            tools.Definitions.Select(definition => definition.Path.Value));
    }

    [Fact]
    public void Whitelist_with_similar_prefix_does_not_match_group()
    {
        ToolCollection tools = new(
        [
            new FakeTool("KuroeInfo/ListTasks", "KuroeInfo/GetTask", "KuroeInfo/GetExecution"),
        ],
        new ToolJson());

        IReadOnlyList<AITool> built = tools.Build(
            new ExecutionId(1), NoSink, new ProcessJournal(), [new ToolPath("KuroeInfoX")]);

        Assert.Empty(built);
    }

    private static readonly NullSink NoSink = new();

    private sealed class FakeTool(params string[] paths) : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
            [
                .. paths.Select(path =>
                    new ToolDefinition(new ToolPath(path), "测试工具", []).Bind(_ => string.Empty)),
            ];
    }

    private sealed class NullSink : IProcessSink
    {
        public void OnText(string delta)
        {
        }

        public void OnToolCall(ToolCallRecord record)
        {
        }
    }
}