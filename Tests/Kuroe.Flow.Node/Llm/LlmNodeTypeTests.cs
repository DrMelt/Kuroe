using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Node.Llm.Tests;

/// <summary>Llm 节点类型：类型名与注册、静态形状、参数段的字段与规则。</summary>
public sealed class LlmNodeTypeTests
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    private static readonly LlmNodeType NodeType = new();

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Llm", LlmNodeTypes.Name.Value);
        Assert.Equal(LlmNodeTypes.Name, NodeType.Kind);
    }

    [Fact]
    public void Registration_adds_the_type_to_a_registry()
    {
        NodeTypeRegistry registry = new();
        LlmNodeTypes.RegisterInto(registry);

        Assert.Equal(LlmNodeTypes.Name, registry.Find("llm")?.Kind);
    }

    [Fact]
    public void Shape_dispatches_runs()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesRuns);
        Assert.False(shape.Organizes);
        Assert.Equal([PortType.Text, PortType.Context, PortType.Signal], shape.InputTypes);
        Assert.Equal([PortType.Text, PortType.WorkItems, PortType.Context], shape.OutputTypes);
    }

    [Fact]
    public void Fields_declare_the_parameter_segment() =>
        Assert.Equal(
            [
                new ParameterField(PromptName, ParameterValueKind.Text),
                new ParameterField(ToolsName, ParameterValueKind.Texts),
                new ParameterField(SystemPromptName, ParameterValueKind.Texts),
            ],
            NodeType.Fields);

    [Fact]
    public void Read_builds_the_parameters_from_the_values()
    {
        LlmParameters parameters = Assert.IsType<LlmParameters>(NodeType.Read(
            new Reader("围绕目标展开", ["ReadFile", "files/WriteFile"], ["恒定块"]),
            _ => { }));

        Assert.Equal("围绕目标展开", parameters.Prompt);
        Assert.Equal(["ReadFile", "files/WriteFile"], parameters.Tools.Select(tool => tool.Value));
        Assert.Equal(["恒定块"], parameters.SystemPrompt);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        LlmParameters parameters = Assert.IsType<LlmParameters>(NodeType.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Prompt);
        Assert.Empty(parameters.Tools);
        Assert.Empty(parameters.SystemPrompt);
    }

    [Fact]
    public void Read_reports_an_illegal_capability_path()
    {
        List<string> errors = [];
        NodeType.Read(new Reader(tools: ["files/写入 文件"]), errors.Add);

        Assert.Equal(["路径 files/写入 文件 的段 写入 文件 不能为空或含空白与花括号。"], errors);
    }

    [Fact]
    public void Read_reports_a_blank_system_block()
    {
        List<string> errors = [];
        NodeType.Read(new Reader(blocks: ["恒定块", " "]), errors.Add);

        Assert.Equal(["系统指令块不能为空。"], errors);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(NodeType.Write(new LlmParameters()).Values);

        ParameterValues values = NodeType.Write(new LlmParameters
        {
            Prompt = "围绕目标展开",
            Tools = [new CapabilityPath("ReadFile")],
        });

        Assert.Equal(
            [(PromptName, ParameterValueKind.Text), (ToolsName, ParameterValueKind.Texts)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("围绕目标展开", values.Values[0].Text);
        Assert.Equal(["ReadFile"], values.Values[1].Texts);
    }

    [Fact]
    public void Max_runs_below_one_is_rejected()
    {
        List<string> errors = [];
        NodeType.Validate(Node(new ExecutableSpec { MaxRuns = 0 }), NodeScope.Flow("测试"), error => errors.Add(error.Description));

        Assert.Equal(["流程 测试 的节点 干活：MaxRuns 必须是正整数。"], errors);
    }

    private static NodeSpec Node(ExecutableSpec execution) => new()
    {
        Name = new NodeName("干活"),
        Kind = LlmNodeTypes.Name,
        Outputs = [new Port(new PortName("结论"), PortType.Text)],
        Execution = execution,
    };

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(
        string? prompt = null,
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? blocks = null) : IParameterReader
    {
        public string? Text(string name) => name == PromptName ? prompt : null;

        public IReadOnlyList<string> Texts(string name) => name switch
        {
            ToolsName => tools ?? [],
            SystemPromptName => blocks ?? [],
            _ => [],
        };
    }
}