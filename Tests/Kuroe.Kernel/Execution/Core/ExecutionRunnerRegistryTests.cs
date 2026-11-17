using ErrorOr;
using Xunit;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Tests;

/// <summary>运行器选择：按执行器工厂类型精确匹配，没有匹配时拒绝。</summary>
public sealed class ExecutionRunnerRegistryTests
{
    private static readonly ExecutionId ExecutionKey = new(1);

    [Fact]
    public void Runner_is_selected_by_factory_type()
    {
        NamedFactory alpha = new("alpha");
        NamedFactory beta = new("beta");
        ExecutionRunnerRegistry registry = new(
        [
            new ExecutionRunnerRegistration(typeof(IAlphaFactory), alpha),
            new ExecutionRunnerRegistration(typeof(IBetaFactory), beta),
        ]);

        Assert.Equal(
            "beta",
            ((NamedRunner)registry.Create(Target(typeof(IBetaFactory)), ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_runner_is_rejected()
    {
        ExecutionRunnerRegistry registry = new();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Create(Target(typeof(IAlphaFactory)), ExecutionKey));
    }

    [Fact]
    public void Registered_factory_can_be_found_by_type()
    {
        NamedFactory alpha = new("alpha");
        ExecutionRunnerRegistry registry = new([new ExecutionRunnerRegistration(typeof(IAlphaFactory), alpha)]);

        Assert.Same(alpha, registry.Find(typeof(IAlphaFactory)));
        Assert.Null(registry.Find(typeof(IBetaFactory)));
    }

    private static FakeTarget Target(Type runnerFactoryType) => new(runnerFactoryType);

    private sealed record FakeTarget(Type RunnerFactoryType) : IExecutionTarget
    {
        public ExecutionTargetId Id => new("target");

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }

    private sealed class NamedFactory(string name) : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) => new NamedRunner(name);
    }

    private sealed record NamedRunner(string Name) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));
    }

    private interface IAlphaFactory
    {
    }

    private interface IBetaFactory
    {
    }
}