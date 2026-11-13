using Kuroe.Flow.Execution.Shared;
using Kuroe.Node.Llm;
using Kuroe.Shared;
using Kuroe.Tool;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests.Tools;

/// <summary>执行级工具工厂：按最长工具路径绑定变量，同一工具源的不同工具得到不同映射。</summary>
public sealed class KuroeToolFactoryTests
{
    [Fact]
    public void Longest_prefix_binds_each_tool_to_its_own_mapping()
    {
        CapturingToolSource source = new(
            new ToolDefinition(new ToolPath("group/tool"), "父", [], ["root"]),
            new ToolDefinition(new ToolPath("group/sub/tool"), "子", [], ["project"]));

        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("group")],
            ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["group"] = new Dictionary<string, string> { [PathVariables.RootName] = "任务路径" },
                ["group/sub"] = new Dictionary<string, string> { ["project"] = "项目路径" },
            },
        };
        ExecutionRequest request = Request(new Dictionary<ExecutionPortName, string>
        {
            [new ExecutionPortName("任务路径")] = "任务值",
            [new ExecutionPortName("项目路径")] = "项目值",
        });

        IReadOnlyList<ITool> tools = new KuroeToolFactory([source], "C:/workspace")
            .Create(
                new ExecutionId(1),
                request,
                new LlmExecutionParameters(parameters, null, LlmNodeTypes.Kind));
        CapturingTool parent = Assert.IsType<CapturingTool>(
            tools.Single(tool => tool.Functions.Single().Definition.Path.Value == "group/tool"));
        CapturingTool child = Assert.IsType<CapturingTool>(
            tools.Single(tool => tool.Functions.Single().Definition.Path.Value == "group/sub/tool"));

        Assert.Equal("任务值", parent.Binding.Variable("root"));
        Assert.Equal("项目值", child.Binding.Variable("project"));
    }

    [Fact]
    public void A_static_port_reads_only_the_static_value()
    {
        CapturingToolSource source = new(new ToolDefinition(new ToolPath("group/tool"), "父", [], [PathVariables.RootName]));
        ExecutionRequest request = ToolInput(staticPort: true, "任务值", "动态值");
        LlmParameters parameters = ToolParameters();

        CapturingTool tool = Assert.IsType<CapturingTool>(Assert.Single(
            new KuroeToolFactory([source], "C:/workspace").Create(
                new ExecutionId(1),
                request,
                new LlmExecutionParameters(parameters, null, LlmNodeTypes.Kind))));

        Assert.Equal("任务值", tool.Binding.Variable(PathVariables.RootName));
    }

    [Fact]
    public void A_dynamic_port_reads_only_the_input_value()
    {
        CapturingToolSource source = new(new ToolDefinition(new ToolPath("group/tool"), "父", [], [PathVariables.RootName]));
        ExecutionRequest request = ToolInput(staticPort: false, "静态值", "动态值");
        LlmParameters parameters = ToolParameters();

        CapturingTool tool = Assert.IsType<CapturingTool>(Assert.Single(
            new KuroeToolFactory([source], "C:/workspace").Create(
                new ExecutionId(1),
                request,
                new LlmExecutionParameters(parameters, null, LlmNodeTypes.Kind))));

        Assert.Equal("动态值", tool.Binding.Variable(PathVariables.RootName));
    }

    private static ExecutionRequest ToolInput(bool staticPort, string staticText, string inputText)
    {
        ExecutionPortName port = new("任务路径");

        return Request(new Dictionary<ExecutionPortName, string>
        {
            [port] = staticPort ? staticText : inputText,
        });
    }

    private static LlmParameters ToolParameters() => new()
    {
        Tools = [new ToolPath("group")],
        ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["group"] = new Dictionary<string, string> { [PathVariables.RootName] = "任务路径" },
        },
    };

    private static ExecutionRequest Request(IReadOnlyDictionary<ExecutionPortName, string> texts) =>
        new(
            new ExecutionOwnerId("1"),
            new ExecutionTargetId("流程/节点"),
            "Llm",
            1,
            [],
            [],
            texts,
            []);

    [Fact]
    public void A_source_creates_only_tools_in_the_available_face()
    {
        ToolDefinition one = new(new ToolPath("group/one"), "一", []);
        ToolDefinition two = new(new ToolPath("group/two"), "二", []);
        FilteringToolSource source = new(
            ToolEntry.Of(one, () => new TestTool(one)),
            ToolEntry.Of(two, () => new TestTool(two)));
        ToolContext context = new(
            new FixedPortAccess(new ExecutionPortName(PathVariables.RootName), "C:/workspace"),
            new Dictionary<string, IReadOnlyDictionary<string, ExecutionPortName>>(),
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