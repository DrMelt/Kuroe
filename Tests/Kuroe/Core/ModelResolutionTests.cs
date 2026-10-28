using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Node.Llm;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行节点到可用模型的解析：静态值来源、模型名解析与目录可达性各分支的判定。</summary>
public sealed class ModelResolutionTests
{
    [Fact]
    public void Node_without_static_model_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(environment).For(Executable(model: null), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("节点 干活 没有静态端口 MODEL_NAME，无法确定使用的模型。", resolved.FirstError.Description);
    }

    [Fact]
    public void Constant_model_resolves_a_registered_model()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(environment)
            .For(Executable(StaticValue.Constant("fake")), Host());

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Constant_model_of_an_unregistered_model_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(environment)
            .For(Executable(StaticValue.Constant("nope")), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("Catalog.ModelNotFound", resolved.FirstError.Code);
    }

    [Fact]
    public void Host_model_resolves_the_task_start_value()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(environment)
            .For(Executable(StaticValue.Host(LlmNodeTypes.ModelPort)), Host(model: "fake"));

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Host_model_without_a_task_value_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(environment)
            .For(Executable(StaticValue.Host(LlmNodeTypes.ModelPort)), Host());

        Assert.True(resolved.IsError);
        Assert.Equal("节点 干活 的静态端口 MODEL_NAME 没有任务启动值，无法确定使用的模型。", resolved.FirstError.Description);
    }

    private static NodeModelResolver Resolver(TestEnvironment environment) => new(environment.Catalog);

    private static KuroeNodeDeclaration Declaration(ErrorOr<INodeDeclaration> resolved) =>
        Assert.IsType<KuroeNodeDeclaration>(resolved.Value);

    private static HostStartValues Host(string? model = null) =>
        new(model is null
            ? []
            : new Dictionary<PortName, string> { [LlmNodeTypes.ModelPort] = model });

    private static ExecutableNode Executable(StaticValue? model) => new()
    {
        Path = NodePath.Of("干活"),
        Type = LlmNodeTypes.Create(null),
        Spec = new ExecutableSpec(),
        Inputs = [],
        StaticValues = model is { } value
            ? new Dictionary<PortName, StaticValue> { [LlmNodeTypes.ModelPort] = value }
            : null,
        Outputs = [],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    };
}
