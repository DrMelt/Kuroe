using ErrorOr;
using Kuroe.Execution.Shared;
using Xunit;

namespace Kuroe.Execution.Tests;

/// <summary>运行器选择：同种类先于通配，没有匹配时拒绝。</summary>
public sealed class ExecutionRunnerRegistryTests
{
    private static readonly ExecutionId ExecutionKey = new(1);

    [Fact]
    public void Same_kind_runner_wins_over_wildcard()
    {
        NamedFactory wildcard = new("*");
        NamedFactory kind = new("kind");
        ExecutionRunnerRegistry registry = new([wildcard, kind]);

        Assert.Equal("kind", ((NamedRunner)registry.Create(Target("kind"), ExecutionKey)).Name);
    }

    [Fact]
    public void Wildcard_runner_is_the_last_resort()
    {
        NamedFactory wildcard = new("*");
        ExecutionRunnerRegistry registry = new([wildcard]);

        Assert.Equal("*", ((NamedRunner)registry.Create(Target("other"), ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_runner_is_rejected()
    {
        ExecutionRunnerRegistry registry = new();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Create(Target("other"), ExecutionKey));
    }

    private static FakeTarget Target(string kind) => new(kind);

    private sealed record FakeTarget(string Kind) : IExecutionTarget
    {
        public ExecutionTargetId Id => new("target");

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }

    private sealed class NamedFactory(string name) : IExecutionRunnerFactory
    {
        public string Kind => name;

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