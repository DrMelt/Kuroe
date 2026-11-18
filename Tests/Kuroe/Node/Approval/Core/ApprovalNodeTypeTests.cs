using ErrorOr;
using Kuroe.Flow.Node.Shared.StaticGraph.Validation;
using Kuroe.Flow.Node.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Xunit;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点类型的形状与类型规则：启动执行后等待批准，对外端口由输入端口派生，放行后放出停驻时的取值。</summary>
public sealed class ApprovalNodeTypeTests
{
    private static readonly IExecutionRunnerFactory RunnerFactory = new UnusedRunnerFactory();

    private static readonly ApprovalNodeType NodeType = new(null, RunnerFactory);

    [Fact(DisplayName = "审批节点名称是文件书写的种类")]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Approval", ApprovalNodeTypes.Name.Value);
        Assert.Equal(ApprovalNodeTypes.Name, NodeType.Kind);
    }

    [Fact(DisplayName = "审批类型创建把声明保留在实例上")]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType type = ApprovalNodeTypes.Create(new ApprovalParameters { Question = "确认计划" }, RunnerFactory);

        Assert.Equal(ApprovalNodeTypes.Name, type.Kind);
        Assert.Equal("确认计划", Assert.IsType<ApprovalNodeType>(type).Parameters?.Question);
    }

    [Fact(DisplayName = "形状推导端口并派发执行")]
    public void Shape_derives_its_ports_and_dispatches_execution()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.True(shape.DispatchesExecutions);
        Assert.True(shape.AwaitsExternalInput);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.IsType<OutputDeclaration.DerivedFromInputs>(shape.Outputs);
        Assert.Equal([PortTypes.Text, PortTypes.Context, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text, PortTypes.Context, PortTypes.Signal], shape.OutputTypes);
    }

    [Fact(DisplayName = "含内容端口的节点通过")]
    public void Node_with_content_port_passes() =>
        Assert.Empty(Failures(Node([new InputPort(new PortName("计划"), PortTypes.Text)])));

    [Fact(DisplayName = "没有输入端口的节点被拒绝")]
    public void Node_without_input_port_is_rejected() =>
        Assert.Equal(["流程 测试 的节点 把关：审批节点必须声明输入端口，对外端口取自身输入端口。"], Failures(Node()));

    [Fact(DisplayName = "第二个上下文输入端口被拒绝")]
    public void Second_context_input_port_is_rejected() =>
        Assert.Equal(
            ["流程 测试 的节点 把关：审批节点至多声明一个 Context 输入端口。"],
            Failures(Node(
            [
                new InputPort(new PortName("甲"), PortTypes.Context),
                new InputPort(new PortName("乙"), PortTypes.Context),
            ])));

    [Fact(DisplayName = "执行配置被拒绝")]
    public void Execution_config_is_rejected()
    {
        NodeSpec node = Node([new InputPort(new PortName("计划"), PortTypes.Text)]) with
        {
            MaxExecutions = 3,
        };

        string[] errors = [.. Failures(node)];

        Assert.Single(errors);
        Assert.Contains(errors, error => error.EndsWith("审批节点不能声明 MaxExecutions。"));
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
        Type = ApprovalNodeTypes.Create(null, RunnerFactory),
        Inputs = inputs ?? [],
        Spec = new ExecutableSpec(),
    };

    private sealed class UnusedRunnerFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
