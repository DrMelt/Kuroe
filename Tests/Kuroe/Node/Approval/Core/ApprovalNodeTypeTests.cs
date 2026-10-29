using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点类型的形状与类型规则：不启动执行，对外端口由输入端口派生，放行后放出停驻时的取值。</summary>
public sealed class ApprovalNodeTypeTests
{
    private static readonly StubInputChannel Channel = new();
    private static readonly ApprovalNodeType NodeType = new(null, Channel);

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Approval", ApprovalNodeTypes.Name.Value);
        Assert.Equal(ApprovalNodeTypes.Name, NodeType.Kind);
    }

    [Fact]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = ApprovalNodeTypes.Create(new ApprovalParameters { Question = "确认计划" }, Channel);

        Assert.Equal(ApprovalNodeTypes.Name, type.Kind);
        Assert.Equal("确认计划", Assert.IsType<ApprovalNodeType>(type).Parameters?.Question);
    }

    [Fact]
    public void Shape_derives_its_ports_without_dispatching_executions()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.False(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.True(shape.DerivesOutputsFromInputs);
        Assert.Equal([PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger], shape.InputTypes);
        Assert.Equal([PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger], shape.OutputTypes);
    }

    [Fact]
    public void Node_with_content_port_passes() =>
        Assert.Empty(Failures(Node([new InputPort(new PortName("计划"), PortTypes.Text)])));

    [Fact]
    public void Node_without_input_port_is_rejected() =>
        Assert.Equal(["流程 测试 的节点 把关：审批节点必须声明输入端口，对外端口取自身输入端口。"], Failures(Node()));

    [Fact]
    public void Second_context_input_port_is_rejected() =>
        Assert.Equal(
            ["流程 测试 的节点 把关：审批节点至多声明一个 Context 输入端口。"],
            Failures(Node(
            [
                new InputPort(new PortName("甲"), PortTypes.Context),
                new InputPort(new PortName("乙"), PortTypes.Context),
            ])));

    [Fact]
    public void Execution_config_is_rejected()
    {
        NodeSpec node = Node([new InputPort(new PortName("计划"), PortTypes.Text)]) with
        {
            MaxExecutions = 3,
            Spec = new ExecutableSpec
            {
                Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
            },
        };

        string[] errors = [.. Failures(node)];

        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动执行，不能声明 Validate。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动执行，不能声明 MaxExecutions。"));
    }

    private static IReadOnlyList<string> Failures(NodeSpec node)
    {
        List<Error> errors = [];
        NodeType.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static NodeSpec Node(IReadOnlyList<InputPort>? inputs = null) => new()
    {
        Label = new NodeLabel("把关"),
        Type = ApprovalNodeTypes.Create(null, Channel),
        Inputs = inputs ?? [],
        Spec = new ExecutableSpec(),
    };
}
