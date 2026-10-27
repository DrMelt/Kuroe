using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;
using Kuroe.Tool.Json;
using Kuroe.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行级工具工厂：按最长工具路径绑定变量，同一工具源的不同工具得到不同映射。</summary>
public sealed class ToolFactoryTests
{
    [Fact]
    public void Longest_prefix_binds_each_tool_to_its_own_mapping()
    {
        ServiceCollection services = new();
        KuroeComposition composition = new(services, [], new KuroeStartup(LogLevel.Warning));
        CapturingToolSource source = new(
            new ToolDefinition(new ToolPath("group/tool"), "父", [], ["root"]),
            new ToolDefinition(new ToolPath("group/sub/tool"), "子", [], ["project"]));
        Assert.False(composition.AddToolSources([source]).IsError);

        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("group")],
            ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["group"] = new Dictionary<string, string> { [PathVariables.RootName] = "任务路径" },
                ["group/sub"] = new Dictionary<string, string> { ["project"] = "项目路径" },
            },
        };
        ExecutionInput input = new()
        {
            Task = new TaskId(1),
            Node = NodePath.Of("流程", "节点"),
            Type = new LlmNodeType(parameters),
            Inputs =
            [
                new InputValue(new PortName("任务路径"), Value("任务值")),
                new InputValue(new PortName("项目路径"), Value("项目值")),
            ],
        };

        IReadOnlyList<ITool> tools = new KuroeToolFactory([source], "C:/workspace")
            .Create(new ExecutionId(1), input);
        CapturingTool parent = Assert.IsType<CapturingTool>(
            tools.Single(tool => tool.Functions.Single().Definition.Path.Value == "group/tool"));
        CapturingTool child = Assert.IsType<CapturingTool>(
            tools.Single(tool => tool.Functions.Single().Definition.Path.Value == "group/sub/tool"));

        Assert.Equal("任务值", parent.Binding.Variable("root"));
        Assert.Equal("项目值", child.Binding.Variable("project"));
    }

    private static ContextValue Value(string text) =>
        new(new NodeSource(NodePath.Of("来源", "节点"), new PortName("结果")), text);

    [Fact]
    public void A_source_creates_only_tools_in_the_available_face()
    {
        ToolDefinition one = new(new ToolPath("group/one"), "一", []);
        ToolDefinition two = new(new ToolPath("group/two"), "二", []);
        FilteringToolSource source = new(
            ToolEntry.Of(one, () => new TestTool(one)),
            ToolEntry.Of(two, () => new TestTool(two)));
        ToolContext context = new(
            new FixedPortAccess(new PortName(PathVariables.RootName), "C:/workspace"),
            new Dictionary<string, IReadOnlyDictionary<string, PortName>>(),
            "C:/workspace",
            new ExecutionId(1),
            [new ToolPath("group/one")]);

        IReadOnlyList<ITool> tools = source.Create(context);

        Assert.Equal("group/one", Assert.Single(tools).Functions[0].Definition.Path.Value);
    }

    private sealed class FilteringToolSource(params ToolEntry[] entries) : ToolSource(entries)
    {
    }

    private sealed class TestTool(ToolDefinition definition) : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } = [definition.Bind(_ => "ok")];
    }

    private sealed class CapturingToolSource(params ToolDefinition[] definitions) : IToolSource
    {
        public IReadOnlyList<ToolDefinition> Definitions { get; } = definitions;

        public IReadOnlyList<ITool> Create(ToolContext context) =>
            [.. Definitions.Select(definition => new CapturingTool(definition, context.For(definition.Path.Value)))];
    }

    private sealed class CapturingTool(ToolDefinition definition, ToolBinding binding) : ITool
    {
        public ToolBinding Binding { get; } = binding;

        public IReadOnlyList<ToolFunction> Functions { get; } = [definition.Bind(_ => "ok")];
    }
}