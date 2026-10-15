using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Flow.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>执行节点到可用模型的解析：引用缺失、运行时选择与目录可达性各分支的判定。</summary>
public sealed class ModelResolutionTests
{
    [Fact]
    public void Node_without_model_choice_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(Executable(model: null));

        Assert.True(resolved.IsError);
        Assert.Equal("节点 干活 没有模型选择，无法确定使用的模型。", resolved.FirstError.Description);
    }

    [Fact]
    public void Named_choice_without_a_model_name_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(
            Executable(new ModelDefinition { Name = new ModelRef("执行者") }));

        Assert.True(resolved.IsError);
        Assert.Equal("模型选择 执行者 未写 Model，无法确定使用的模型。", resolved.FirstError.Description);
    }

    [Fact]
    public void Named_choice_resolves_a_registered_model()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(
            Executable(new ModelDefinition { Name = new ModelRef("执行者"), Model = "fake" }));

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Named_choice_of_an_unregistered_model_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(
            Executable(new ModelDefinition { Name = new ModelRef("执行者"), Model = "nope" }));

        Assert.True(resolved.IsError);
        Assert.Equal("Catalog.ModelNotFound", resolved.FirstError.Code);
    }

    [Fact]
    public void Runtime_choice_uses_the_selected_model()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(
            Executable(new ModelDefinition { Name = new ModelRef("执行者"), Runtime = true }));

        Assert.False(resolved.IsError);
        Assert.Equal("fake", Declaration(resolved).Model.Value);
    }

    [Fact]
    public void Runtime_choice_without_a_selection_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Models.Unselect().ThrowIfError();

        ErrorOr<INodeDeclaration> resolved = Resolver(harness).For(
            Executable(new ModelDefinition { Name = new ModelRef("执行者"), Runtime = true }));

        Assert.True(resolved.IsError);
        Assert.Equal("当前未选择模型。", resolved.FirstError.Description);
    }

    private static NodeModelResolver Resolver(KuroeHarness harness) => new(harness.Catalog, harness.Settings);

    private static KuroeNodeDeclaration Declaration(ErrorOr<INodeDeclaration> resolved) =>
        Assert.IsType<KuroeNodeDeclaration>(resolved.Value);

    private static ExecutableNode Executable(ModelDefinition? model) => new()
    {
        Index = 1,
        Name = new NodeName("干活"),
        Path = new NodePath([new NodeName("干活")]),
        Kind = BuiltinNodeKinds.Llm,
        Execution = new ExecutableSpec(),
        Model = model,
        Outputs = [],
        Parameters = null,
        MaxRuns = ExecutableNode.DefaultMaxRuns,
    };
}
