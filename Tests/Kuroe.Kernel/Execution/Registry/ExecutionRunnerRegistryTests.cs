using ErrorOr;
using Xunit;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry.Tests;

/// <summary>运行器选择：按执行运行器键精确匹配，没有匹配或键重复时拒绝。</summary>
public sealed class ExecutionRunnerRegistryTests
{
    private static readonly ExecutionId ExecutionKey = new(1);

    [Fact]
    public void Runner_is_selected_by_key()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        NamedFactory beta = new(new("beta"), "beta");
        ExecutionRunnerRegistry registry = new([alpha, beta]);

        Assert.Equal(
            "beta",
            ((NamedRunner)registry.Create(Target(new("beta")), ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_runner_is_rejected()
    {
        ExecutionRunnerRegistry registry = new();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Create(Target(new("alpha")), ExecutionKey));
    }

    [Fact]
    public void Registered_factory_can_be_found_by_key()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        ExecutionRunnerRegistry registry = new([alpha]);

        Assert.Same(alpha, registry.Find(new("alpha")));
        Assert.Null(registry.Find(new("beta")));
    }

    [Fact]
    public void Duplicate_key_is_rejected()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        NamedFactory beta = new(new("alpha"), "beta");

        Assert.Throws<InvalidOperationException>(() =>
            new ExecutionRunnerRegistry([alpha, beta]));
    }

    private static FakeTarget Target(ExecutionRunnerKey key) => new(key);

    private sealed record FakeTarget(ExecutionRunnerKey RunnerKey) : IExecutionTarget
    {
        public ExecutionTargetId Id => new("target");

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }

    private sealed class NamedFactory(ExecutionRunnerKey key, string name) : IExecutionRunnerFactory
    {
        public ExecutionRunnerKey Key => key;

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
}