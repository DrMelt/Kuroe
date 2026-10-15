using ErrorOr;
using Kuroe.Flows.Shared;
using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Node.Approval.Tests;

/// <summary>审批节点类型的形状与类型规则：不启动 run，对外端口由输入端口派生，批准后转发产出。</summary>
public sealed class ApprovalNodeTypeTests
{
    private static readonly ApprovalNodeType NodeType = new();

    [Fact]
    public void Name_is_the_kind_written_in_files()
    {
        Assert.Equal("Approval", ApprovalNodeTypes.Name.Value);
        Assert.Equal(ApprovalNodeTypes.Name, NodeType.Type);
    }

    [Fact]
    public void Registration_adds_the_type_to_a_registry()
    {
        NodeTypeRegistry registry = new();
        ApprovalNodeTypes.RegisterInto(registry);

        Assert.Equal(ApprovalNodeTypes.Name, registry.Find("approval")?.Type);
    }

    [Fact]
    public void Shape_derives_its_ports_without_dispatching_runs()
    {
        NodeTypeShape shape = NodeType.Shape;

        Assert.False(shape.DispatchesRuns);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.True(shape.DerivesOutputsFromInputs);
        Assert.Equal([PortType.Text, PortType.Context, PortType.Signal], shape.InputTypes);
        Assert.Equal([PortType.Text, PortType.Context, PortType.Signal], shape.OutputTypes);
    }

    [Fact]
    public void Node_with_content_port_passes() =>
        Assert.Empty(Failures(Node([new InputPort(new PortName("计划"), PortType.Text)])));

    [Fact]
    public void Node_without_input_port_is_rejected() =>
        Assert.Equal(["流程 测试 的节点 把关：审批节点必须声明输入端口，对外端口取自身输入端口。"], Failures(Node()));

    [Fact]
    public void Second_context_input_port_is_rejected() =>
        Assert.Equal(
            ["流程 测试 的节点 把关：审批节点至多声明一个 Context 输入端口。"],
            Failures(Node(
            [
                new InputPort(new PortName("甲"), PortType.Context),
                new InputPort(new PortName("乙"), PortType.Context),
            ])));

    [Fact]
    public void Execution_config_and_model_are_rejected()
    {
        NodeSpec node = Node([new InputPort(new PortName("计划"), PortType.Text)]) with
        {
            Model = new ModelRef("规划者"),
            MaxRuns = 3,
            SystemPrompt = ["指令"],
            Execution = new ExecutableSpec
            {
                Tools = [new CapabilityPath("shell")],
                Prompt = "写点什么",
                Mode = NodeMode.PerItem,
                Branch = new BranchName("甲"),
                Validate = new OutputValidation(ValidationPredicate.NonEmpty, null),
            },
        };

        string[] errors = [.. Failures(node)];

        Assert.Equal(8, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明模型选择。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明 Tools。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明 Prompt。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点只能整节点等待批准。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明 Branch。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明 Validate。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明系统指令。"));
        Assert.Contains(errors, error => error.EndsWith("审批节点不启动 run，不能声明 MaxRuns。"));
    }

    private static IReadOnlyList<string> Failures(NodeSpec node)
    {
        List<Error> errors = [];
        NodeType.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static NodeSpec Node(IReadOnlyList<InputPort>? inputs = null) => new()
    {
        Name = new NodeName("把关"),
        Kind = ApprovalNodeTypes.Name,
        Inputs = inputs ?? [],
        Execution = new ExecutableSpec(),
    };
}
