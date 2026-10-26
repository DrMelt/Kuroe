using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Process;
using Xunit;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点类型的形状与类型规则：不启动模型执行，按版本执行工作目录内的命令。</summary>
public sealed class CommandNodeTypeTests
{
    private static readonly CommandParameters Parameters = new()
    {
        Program = "dotnet",
        Arguments = ["build"],
        Directory = null,
        RootDirectory = Path.GetFullPath("."),
        TimeoutSeconds = CommandNodeTypes.DefaultTimeoutSeconds,
        MaxChars = CommandNodeTypes.DefaultMaxChars,
    };

    private static readonly FakeProcessRunner Process = new();
    private static readonly CommandNodeType Type = new(Parameters, Process);

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Command", CommandNodeTypes.Name.Value);
        Assert.Equal(CommandNodeTypes.Name, Type.Kind);
    }

    [Fact]
    public void Shape_takes_text_and_signal_inputs_and_produces_text()
    {
        NodeTypeShape shape = Type.Shape;

        Assert.False(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text], shape.OutputTypes);
    }

    [Fact]
    public void Create_keeps_the_declaration_and_process_on_the_instance()
    {
        INodeType type = CommandNodeTypes.Create(Parameters, Process);

        CommandNodeType command = Assert.IsType<CommandNodeType>(type);
        Assert.Same(Parameters, command.Parameters);
        Assert.Same(Process, command.Process);
    }

    [Fact]
    public void Plain_command_node_passes() => Assert.Empty(Failures(Type, Node()));

    [Fact]
    public void Model_and_validate_are_rejected()
    {
        NodeSpec node = Node() with
        {
            Model = new ModelRef("执行者"),
            Spec = new ExecutableSpec { Validate = new OutputValidation(ValidationPredicate.NonEmpty, null) },
        };

        string[] errors = [.. Failures(Type, node)];

        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("命令执行节点不启动模型执行，不能声明模型选择。"));
        Assert.Contains(errors, error => error.EndsWith("命令执行节点不启动模型执行，不能声明 Validate。"));
    }

    [Fact]
    public void Trigger_and_output_are_required()
    {
        string[] errors = [.. Failures(Type, Node() with { Inputs = [], Outputs = [] })];

        Assert.Contains(errors, error => error.EndsWith("命令执行节点必须声明至少一个 Signal 输入端口作为触发。"));
        Assert.Contains(errors, error => error.EndsWith("命令执行节点必须声明 BasePath 输入端口作为路径解析基准。"));
        Assert.Contains(errors, error => error.EndsWith("命令执行节点必须声明一个输出端口，命令输出写入该端口。"));
    }

    [Fact]
    public void Text_input_besides_base_path_is_rejected()
    {
        InputPort[] inputs =
        [
            new(CommandNodePorts.BasePath, PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
            new(new PortName("说明"), PortTypes.Text),
        ];

        Assert.Contains(Failures(Type, Node(inputs)),
            error => error.EndsWith("命令执行节点只有 BasePath 可以是 Text 输入端口。"));
    }

    [Fact]
    public void A_signal_base_path_is_rejected()
    {
        InputPort[] inputs =
        [
            new(CommandNodePorts.BasePath, PortTypes.Signal),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(Type, Node(inputs)),
            error => error.EndsWith("BasePath 输入端口只能是 Text。"));
    }

    [Fact]
    public void More_than_one_output_is_rejected()
    {
        Port[] outputs =
        [
            new(new PortName("甲"), PortTypes.Text),
            new(new PortName("乙"), PortTypes.Text),
        ];

        Assert.Contains(Failures(Type, Node(outputs: outputs)),
            error => error.EndsWith("命令执行节点必须声明一个输出端口，命令输出写入该端口。"));
    }

    [Fact]
    public void MaxExecutions_must_be_positive()
    {
        NodeSpec node = Node() with { Spec = new ExecutableSpec { MaxExecutions = 0 } };

        Assert.Contains(Failures(Type, node), error => error.EndsWith("MaxExecutions 必须是正整数。"));
    }

    private static IReadOnlyList<string> Failures(CommandNodeType type, NodeSpec node)
    {
        List<Error> errors = [];
        type.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static NodeSpec Node(InputPort[]? inputs = null, Port[]? outputs = null) => new()
    {
        Label = new NodeLabel("执行"),
        Type = Type,
        Spec = new ExecutableSpec(),
        Inputs = inputs
            ?? [
                new InputPort(CommandNodePorts.BasePath, PortTypes.Text),
                new InputPort(new PortName("触发"), PortTypes.Signal),
            ],
        Outputs = outputs ?? [new Port(new PortName("输出"), PortTypes.Text)],
    };

    /// <summary>测试用的进程执行替身，类型规则阶段不会调用。</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public ProcessResult Run(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout) =>
            new(string.Empty, 0, TimedOut: false);
    }
}