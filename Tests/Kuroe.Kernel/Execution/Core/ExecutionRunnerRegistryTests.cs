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
        NamedFactory wildcard = new(ExecutionKind.Any);
        NamedFactory kind = new(new ExecutionKind("kind"));
        ExecutionRunnerRegistry registry = new([wildcard, kind]);

        Assert.Equal(
            new ExecutionKind("kind"),
            ((NamedRunner)registry.Create(Target(new ExecutionKind("kind")), ExecutionKey)).Name);
    }

    [Fact]
    public void Wildcard_runner_is_the_last_resort()
    {
        NamedFactory wildcard = new(ExecutionKind.Any);
        ExecutionRunnerRegistry registry = new([wildcard]);

        Assert.Equal(
            ExecutionKind.Any,
            ((NamedRunner)registry.Create(Target(new ExecutionKind("other")), ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_runner_is_rejected()
    {
        ExecutionRunnerRegistry registry = new();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Create(Target(new ExecutionKind("other")), ExecutionKey));
    }

    private static FakeTarget Target(ExecutionKind kind) => new(kind);

    private sealed record FakeTarget(ExecutionKind Kind) : IExecutionTarget
    {
        public ExecutionTargetId Id => new("target");

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }

    private sealed class NamedFactory(ExecutionKind name) : IExecutionRunnerFactory
    {
        public ExecutionKind Kind => name;

        public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) => new NamedRunner(name);
    }

    private sealed record NamedRunner(ExecutionKind Name) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));
    }
}