using ErrorOr;
using Xunit;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry.Tests;

/// <summary>执行装配：按执行运行器键精确匹配工厂，没有匹配或键重复时拒绝。</summary>
public sealed class ExecutionInvocationFactoryTests
{
    private static readonly ExecutionId ExecutionKey = new(1);

    [Fact]
    public void Invocation_carries_the_factory_selected_by_key()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        NamedFactory beta = new(new("beta"), "beta");
        ExecutionInvocationFactory factory = new([alpha, beta]);

        ErrorOr<ExecutionInvocation> created = factory.Create(Target(new("beta")));
        Assert.False(created.IsError);
        ExecutionInvocation invocation = created.Value;

        Assert.Same(beta, invocation.Factory);
        Assert.Equal(
            "beta",
            ((NamedRunner)invocation.Factory.Create(invocation.Target.Parameters, ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_factory_is_rejected()
    {
        ExecutionInvocationFactory factory = new();

        Assert.True(factory.Create(Target(new("alpha"))).IsError);
    }

    [Fact]
    public void Registered_factory_can_be_found_by_key()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        ExecutionInvocationFactory factory = new([alpha]);

        Assert.Same(alpha, factory.Find(new("alpha")));
        Assert.Null(factory.Find(new("beta")));
    }

    [Fact]
    public void Duplicate_key_is_rejected()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        NamedFactory beta = new(new("alpha"), "beta");

        Assert.Throws<InvalidOperationException>(() =>
            new ExecutionInvocationFactory([alpha, beta]));
    }

    [Fact]
    public void Restore_is_rejected_when_the_factory_does_not_support_it()
    {
        NamedFactory alpha = new(new("alpha"), "alpha");
        ExecutionInvocationFactory factory = new([alpha]);

        Assert.True(factory.PrepareRestore(Context(Target(new("alpha")), new("target"))).IsError);
    }

    [Fact]
    public void Restore_is_rejected_when_the_target_and_request_differ()
    {
        RestoringFactory alpha = new(new("alpha"));
        ExecutionInvocationFactory factory = new([alpha]);

        Assert.True(factory.PrepareRestore(Context(Target(new("alpha")), new("other"))).IsError);
    }

    [Fact]
    public void Restore_carries_the_assembled_invocation_and_data()
    {
        RestoringFactory alpha = new(new("alpha"));
        ExecutionInvocationFactory factory = new([alpha]);
        ExecutionRestoreContext context = Context(Target(new("alpha")), new("target"));

        ErrorOr<PreparedExecutionRestore> prepared = factory.PrepareRestore(context);

        Assert.False(prepared.IsError);
        Assert.Same(alpha, prepared.Value.Invocation.Factory);
        Assert.Same(context, prepared.Value.Context);
        Assert.IsType<TestRestoreData>(prepared.Value.Data);
    }

    private static ExecutionTarget Target(ExecutionRunnerKey key) =>
        new(new ExecutionTargetId("target"), key, EmptyExecutionParameters.Instance);

    private static ExecutionRestoreContext Context(ExecutionTarget target, ExecutionTargetId requestTarget) =>
        new(
            new ExecutionId(1),
            new ExecutionRequest(
                new ExecutionOwnerId("task"),
                requestTarget,
                [],
                [],
                new Dictionary<ExecutionPortName, string>(),
                []),
            target,
            new ExecutionRestoreState("test", "{}"),
            ExecutionContract.Empty);

    private sealed class NamedFactory(ExecutionRunnerKey key, string name) : IExecutionRunnerFactory
    {
        public ExecutionRunnerKey Key => key;

        public IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution) =>
            new NamedRunner(name);
    }

    private sealed class RestoringFactory(ExecutionRunnerKey key) : IExecutionRunnerFactory, IExecutionRestorer
    {
        public ExecutionRunnerKey Key => key;

        public IExecutionRunner Create(IExecutionParameters parameters, ExecutionId execution) =>
            new NamedRunner("restored");

        public ErrorOr<IExecutionRestoreData> Prepare(ExecutionRestoreContext context) =>
            new TestRestoreData();

        public IExecutionRunner CreateRestored(
            ExecutionRestoreContext context,
            IExecutionRestoreData data) =>
            new NamedRunner("restored");
    }

    private sealed record TestRestoreData : IExecutionRestoreData;

    private sealed record NamedRunner(string Name) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));
    }
}