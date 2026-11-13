using ErrorOr;
using Kuroe.Execution.Shared;
using Xunit;

namespace Kuroe.Execution.Tests;

/// <summary>运行器选择：自带运行器优先，默认运行器只由执行目标声明，同种类先于通配。</summary>
public sealed class ExecutionRunnerRegistryTests
{
    private static readonly ExecutionId ExecutionKey = new(1);

    [Fact]
    public void Own_factory_wins_over_default_and_kind()
    {
        NamedFactory own = new("own");
        NamedFactory fallback = new("fallback");
        NamedFactory kind = new("kind");
        ExecutionRunnerRegistry registry = new([kind], fallback);

        IExecutionRunner runner = registry.Create(own, Target("kind", usesDefaultRunner: true), ExecutionKey);

        var typed = Assert.IsType<NamedRunner>(runner);
        Assert.Equal("own", (typed).Name);
    }

    [Fact]
    public void Default_runner_is_used_only_when_the_target_declares_it()
    {
        NamedFactory fallback = new("fallback");
        NamedFactory kind = new("kind");
        ExecutionRunnerRegistry registry = new([kind], fallback);

        Assert.Equal("fallback", ((NamedRunner)registry.Create(null, Target("kind", usesDefaultRunner: true), ExecutionKey)).Name);
        Assert.Equal("kind", ((NamedRunner)registry.Create(null, Target("kind", usesDefaultRunner: false), ExecutionKey)).Name);
    }

    [Fact]
    public void Same_kind_runner_wins_over_wildcard()
    {
        NamedFactory wildcard = new("*");
        NamedFactory kind = new("kind");
        ExecutionRunnerRegistry registry = new([wildcard, kind]);

        Assert.Equal("kind", ((NamedRunner)registry.Create(null, Target("kind", usesDefaultRunner: false), ExecutionKey)).Name);
    }

    [Fact]
    public void Wildcard_runner_is_the_last_resort()
    {
        NamedFactory wildcard = new("*");
        ExecutionRunnerRegistry registry = new([wildcard]);

        Assert.Equal("*", ((NamedRunner)registry.Create(null, Target("other", usesDefaultRunner: false), ExecutionKey)).Name);
    }

    [Fact]
    public void Missing_runner_is_rejected()
    {
        ExecutionRunnerRegistry registry = new();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Create(null, Target("other", usesDefaultRunner: false), ExecutionKey));
    }

    private static FakeTarget Target(string kind, bool usesDefaultRunner) =>
        new(kind, usesDefaultRunner);

    private sealed record FakeTarget(string Kind, bool UsesDefaultRunner) : IExecutionTarget
    {
        public ExecutionTargetId Id => new("target");

        public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
    }

    private sealed class NamedFactory(string name) : IExecutionRunnerFactory
    {
        public string Kind => name;

        public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) => new NamedRunner(name);
    }

    private sealed class NamedRunner(string name) : IExecutionRunner
    {
        public string Name => name;

        public IProcessJournal Journal { get; } = new EmptyJournal();

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));
    }

    private sealed class EmptyJournal : IProcessJournal
    {
        public IReadOnlyList<JournalEntry> Entries => [];

        public event ProcessContentHandler? ContentAppended
        {
            add => _ = value;
            remove => _ = value;
        }

        public event JournalEntryHandler? EntryAppended
        {
            add => _ = value;
            remove => _ = value;
        }

        public event ProgressTextHandler? ProgressReported
        {
            add => _ = value;
            remove => _ = value;
        }
    }
}