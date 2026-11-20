using ErrorOr;
using Xunit;
using NeutralExecution = Kuroe.Kernel.Execution.ExecutionInstance;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Journal;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;
using Kuroe.Kernel.Execution.Shared.Usage;

namespace Kuroe.Kernel.Execution.Tests;

/// <summary>执行包裹：建立运行器、附加过程记录与用量来源、收口与异常映射。</summary>
public sealed class ExecutionRunTests
{
    [Fact(DisplayName = "运行附加运行器来源并收口成功")]
    public async Task Run_attaches_runner_sources_and_settles_success()
    {
        TestRunner runner = new()
        {
            Usage = new ExecutionUsage(1, 0, new TokenUsage(10, 2, 12, null, null)),
            RestoreData = new ExecutionRestoreState("test", "data"),
        };
        runner.OnExecute = _ =>
        {
            runner.Log.Append("测试", "运行");

            return Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));
        };
        NeutralExecution execution = new(new ExecutionId(1), Request());

        ExecutionRun run = ExecutionRun.Create(execution, Invocation(runner));
        await run.ExecuteAsync();

        ExecutionSnapshot snapshot = execution.Snapshot();
        Assert.Equal(ExecutionState.Succeeded, snapshot.State);
        Assert.Equal("运行", Assert.Single(snapshot.Journal).Text);
        Assert.Equal(runner.Usage, snapshot.Usage);
        Assert.Equal(runner.RestoreData, snapshot.RestoreData);
    }

    [Fact(DisplayName = "运行把运行器错误记录为失败")]
    public async Task Run_records_a_runner_error_as_failure()
    {
        TestRunner runner = new()
        {
            OnExecute = _ => Task.FromResult<ErrorOr<ExecutionOutput>>(
                Error.Failure("Test.Failure", "失败。")),
        };
        NeutralExecution execution = new(new ExecutionId(1), Request());

        ExecutionRun run = ExecutionRun.Create(execution, Invocation(runner));
        await run.ExecuteAsync();

        ExecutionSnapshot snapshot = execution.Snapshot();
        Assert.Equal(ExecutionState.Failed, snapshot.State);
        Assert.Contains("失败。", snapshot.Failures);
    }

    [Fact(DisplayName = "运行把意外异常映射为失败")]
    public async Task Run_maps_an_unexpected_exception_to_failure()
    {
        TestRunner runner = new()
        {
            OnExecute = _ => throw new InvalidOperationException("坏了"),
        };
        NeutralExecution execution = new(new ExecutionId(1), Request());

        ExecutionRun run = ExecutionRun.Create(execution, Invocation(runner));
        await run.ExecuteAsync();

        ExecutionSnapshot snapshot = execution.Snapshot();
        Assert.Equal(ExecutionState.Failed, snapshot.State);
        Assert.Contains("执行失败：坏了", snapshot.Failures);
    }

    [Fact(DisplayName = "运行把取消映射为已中断")]
    public async Task Run_maps_cancellation_to_interrupted()
    {
        TestRunner runner = new()
        {
            OnExecute = token => throw new OperationCanceledException(token),
        };
        NeutralExecution execution = new(new ExecutionId(1), Request());
        execution.Interrupt("测试中断。");

        ExecutionRun run = ExecutionRun.Create(execution, Invocation(runner));
        await run.ExecuteAsync();

        ExecutionSnapshot snapshot = execution.Snapshot();
        Assert.Equal(ExecutionState.Interrupted, snapshot.State);
        Assert.Contains("测试中断。", snapshot.Failures);
    }

    private static ExecutionRequest Request() =>
        new(
            new ExecutionOwnerId("任务"),
            new ExecutionStepId("节点"),
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);

    private static ExecutionInvocation Invocation(IExecutionRunner runner) =>
        new(
            new ExecutionStepId("节点"),
            new TestFactory(runner),
            EmptyExecutionParameters.Instance);

    private sealed class TestFactory(IExecutionRunner runner) : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            runner;
    }

    private sealed class TestRunner : IExecutionRunner, IExecutionUsageSource, IExecutionRestoreDataSource
    {
        private readonly ExecutionJournal _journal = new();

        /// <summary>本次执行的过程记录，测试写入条目后读取快照。</summary>
        public ExecutionJournal Log => _journal;

        public IProcessJournal Journal => _journal;

        /// <summary>测试设定的执行动作。</summary>
        public Func<CancellationToken, Task<ErrorOr<ExecutionOutput>>> OnExecute { get; set; } =
            _ => Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.FromPorts([]));

        public ExecutionUsage Usage { get; set; } = ExecutionUsage.None;

        public ExecutionRestoreState? RestoreData { get; set; }

        public event Action? RestoreDataChanged
        {
            add => _ = value;
            remove => _ = value;
        }

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            CancellationToken cancellationToken) =>
            OnExecute(cancellationToken);
    }
}