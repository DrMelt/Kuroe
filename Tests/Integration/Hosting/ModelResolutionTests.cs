using ErrorOr;
using Kuroe.Flow.Compilation.Graph;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>执行节点到可用模型的解析：静态端口绑定、模型名解析与目录可达性各分支的判定。</summary>
public sealed class ModelResolutionTests
{
    private static readonly NodePath StartPath = new([StartNode.ReservedLabel]);

    [Fact]
    public void Node_without_static_model_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolve(environment, Graph(value: null, declare: false, bind: false), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("节点 干活 没有静态端口 MODEL_NAME，无法确定使用的模型。", resolved.FirstError.Description);
    }

    [Fact]
    public void Constant_model_resolves_a_registered_model()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolve(environment, Graph(value: "fake"), Host());

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Constant_model_of_an_unregistered_model_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolve(environment, Graph(value: "nope"), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("Catalog.ModelNotFound", resolved.FirstError.Code);
    }

    [Fact]
    public void Injected_model_resolves_the_task_start_value()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolve(environment, Graph(value: null), Host(model: "fake"));

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Injected_model_without_a_task_value_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolve(environment, Graph(value: null), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("节点 干活 的静态端口 MODEL_NAME 没有任务启动值，无法确定使用的模型。", resolved.FirstError.Description);
    }

    private static ErrorOr<INodeDeclaration> Resolve(TestEnvironment environment, NodeGraph graph, IPortTexts startTexts)
    {
        ExecutableNode executable = (ExecutableNode)graph[NodePath.Of("干活")];

        return new NodeModelResolver(environment.Catalog)
            .For(executable, new StaticPortValues(graph, startTexts).For(executable));
    }

    private static KuroeNodeDeclaration Declaration(ErrorOr<INodeDeclaration> resolved) =>
        Assert.IsType<KuroeNodeDeclaration>(resolved.Value);

    private static PortTexts Host(string? model = null) =>
        new(model is null
            ? []
            : new Dictionary<PortName, string> { [LlmNodeTypes.ModelPort] = model });

    private static NodeGraph Graph(string? value, bool declare = true, bool bind = true) => new(
    [
        new StartNode
        {
            Path = StartPath,
            Outputs = [new Port(LlmNodeTypes.ModelPort, PortTypes.Text, null, value, mode: PortMode.Constant)],
        },
        new ExecutableNode
        {
            Path = NodePath.Of("干活"),
            Type = LlmNodeTypes.Create(null),
            Spec = new ExecutableSpec(),
            Inputs = declare ? [new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, mode: PortMode.Constant)] : [],
            StaticBindings = bind
                ? new Dictionary<PortName, NodePort> { [LlmNodeTypes.ModelPort] = new NodePort(StartPath, LlmNodeTypes.ModelPort) }
                : null,
            Outputs = [],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
        },
    ], []);
}
