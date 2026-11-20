using Kuroe.FlowDescription.Description.Shared.Reporting;
using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点类型的形状与类型规则：派发执行，不调用模型，按版本执行工作目录内的命令。</summary>
public sealed class CommandNodeTypeTests
{
    private static readonly CommandExecutionParameters Parameters = new()
    {
        Program = "dotnet",
        Arguments = ["build"],
        Directory = null,
        RootDirectory = Path.GetFullPath("."),
        TimeoutSeconds = CommandNodeTypes.DefaultTimeoutSeconds,
        MaxChars = CommandNodeTypes.DefaultMaxChars,
    };

    private static readonly CommandNodeType Type = new(Parameters);

    [Fact(DisplayName = "命令节点名称是文件书写的种类")]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Command", CommandNodeTypes.Name.Value);
        Assert.Equal(CommandNodeTypes.Name, Type.Kind);
    }

    [Fact(DisplayName = "形状以文本与信号输入派发内容输出")]
    public void Shape_dispatches_content_output_with_text_and_signal_inputs()
    {
        NodeTypeShape shape = Type.Shape;

        Assert.True(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.InputTypes);
        OutputDeclaration.Given outputs = Assert.IsType<OutputDeclaration.Given>(shape.Outputs);
        Assert.Equal([new PortName("Output"), new PortName("Completed")], outputs.Ports.Select(port => port.Name));
        Assert.Equal([PortTypes.Text, PortTypes.Signal], outputs.Ports.Select(port => port.Type));
        Assert.Equal([PortMode.Plain, PortMode.Plain], outputs.Ports.Select(port => port.Mode));
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.AllowedOutputTypes);
    }

    [Fact(DisplayName = "命令类型创建把声明保留在实例上")]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = CommandNodeTypes.Create(Parameters);

        CommandNodeType command = Assert.IsType<CommandNodeType>(type);
        Assert.Same(Parameters, command.Parameters);
    }

    [Fact(DisplayName = "普通命令节点通过")]
    public void Plain_command_node_passes() => Assert.Empty(Failures(Type, Node()));

    [Fact(DisplayName = "要求触发")]
    public void Trigger_is_required()
    {
        string[] errors = [.. Failures(Type, Node() with { Inputs = [] })];

        Assert.EndsWith("命令执行节点必须声明至少一个 Signal 输入端口作为触发。", Assert.Single(errors));
    }

    [Fact(DisplayName = "没有变量映射的文本输入被拒绝")]
    public void Text_input_without_a_variable_mapping_is_rejected()
    {
        InputPort[] inputs =
        [
            new(new PortName("触发"), PortTypes.Signal),
            new(new PortName("说明"), PortTypes.Text),
        ];

        Assert.Contains(Failures(Type, Node(inputs)),
            error => error.EndsWith("命令执行节点的 Text 输入必须是命令变量端口。"));
    }

    [Fact(DisplayName = "信号变量端口被拒绝")]
    public void A_signal_variable_port_is_rejected()
    {
        CommandExecutionParameters parameters = Parameters with
        {
            Directory = "{root}/src",
            Variables = new Dictionary<string, string> { ["root"] = "任务路径" },
        };
        CommandNodeType type = new(parameters);
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Signal),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(type, Node(inputs, type: type)),
            error => error.EndsWith("命令变量 root 映射到的输入端口 任务路径 必须是 Text。"));
    }

    [Fact(DisplayName = "实参占位符要求变量映射")]
    public void Argument_placeholders_require_variable_mapping()
    {
        CommandExecutionParameters parameters = Parameters with
        {
            Arguments = ["clone", "--branch", "{branch}"],
        };
        CommandNodeType type = new(parameters);

        Assert.Contains(Failures(type, Node(type: type)),
            error => error.EndsWith("命令占位符 {branch} 没有对应的命令变量映射。"));
    }

    [Fact(DisplayName = "实参占位符接受已映射的文本输入")]
    public void Argument_placeholders_accept_mapped_text_inputs()
    {
        CommandExecutionParameters parameters = Parameters with
        {
            Arguments = ["clone", "{url}"],
            Variables = new Dictionary<string, string> { ["url"] = "仓库地址" },
        };
        CommandNodeType type = new(parameters);
        InputPort[] inputs =
        [
            new(new PortName("仓库地址"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Empty(Failures(type, Node(inputs, type: type)));
    }

    [Fact(DisplayName = "实参未使用的变量被拒绝")]
    public void A_variable_not_used_by_arguments_is_rejected()
    {
        CommandExecutionParameters parameters = Parameters with
        {
            Arguments = ["clone"],
            Variables = new Dictionary<string, string> { ["url"] = "仓库地址" },
        };
        CommandNodeType type = new(parameters);
        InputPort[] inputs =
        [
            new(new PortName("仓库地址"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(type, Node(inputs, type: type)),
            error => error.EndsWith("命令变量 url 没有被 Directory 或 Arguments 引用。"));
    }

    [Fact(DisplayName = "变量名不能包含空白")]
    public void Variable_names_must_not_contain_blanks()
    {
        CommandExecutionParameters parameters = Parameters with
        {
            Arguments = ["build", "{bad name}"],
            Variables = new Dictionary<string, string> { ["bad name"] = "任务路径" },
        };
        CommandNodeType type = new(parameters);
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(type, Node(inputs, type: type)),
            error => error.EndsWith("命令变量名 bad name 不能含空白与花括号。"));
    }

    [Fact(DisplayName = "命令节点执行上限必须为正")]
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

    private static NodeSpec Node(
        InputPort[]? inputs = null,
        CommandNodeType? type = null) => new()
        {
            Label = new NodeLabel("执行"),
            Type = type ?? Type,
            Spec = new ExecutableSpec(),
            Inputs = inputs ?? [new InputPort(new PortName("触发"), PortTypes.Signal)],
            Outputs = [.. Assert.IsType<OutputDeclaration.Given>(CommandNodeTypes.Shape.Outputs).Ports],
        };


}
