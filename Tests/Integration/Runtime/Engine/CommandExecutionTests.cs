using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Process;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>命令节点按执行派发运行：执行期间快照不被占用，暂停与终止能中断在途进程。</summary>
public sealed class CommandExecutionTests
{
    [Fact]
    public async Task Command_execution_keeps_snapshot_responsive_and_pause_interrupts_it()
    {
        BlockingProcessRunner process = new();
        using TestEnvironment environment = TestEnvironment.Create(BlockingCommandFlow, process: process);

        TaskId id = environment.Submit("阻塞命令");
        await process.Started.WaitAsync(TimeSpan.FromSeconds(5));

        TaskSnapshot running = await Task.Run(() => environment.Snapshot(id))
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TaskState.Running, running.State);

        environment.Tasks.Pause(id).ThrowIfError();
        await process.Interrupted.WaitAsync(TimeSpan.FromSeconds(5));

        TaskSnapshot paused = await environment.SettleAsync(id);
        Assert.Equal(TaskState.Paused, paused.State);
    }

    [Fact]
    public async Task Stop_task_interrupts_the_command()
    {
        BlockingProcessRunner process = new();
        using TestEnvironment environment = TestEnvironment.Create(BlockingCommandFlow, process: process);

        TaskId id = environment.Submit("阻塞命令");
        await process.Started.WaitAsync(TimeSpan.FromSeconds(5));

        environment.Tasks.StopTask(id).ThrowIfError();
        await process.Interrupted.WaitAsync(TimeSpan.FromSeconds(5));

        TaskSnapshot stopped = await environment.WaitAsync(
            id,
            snapshot => snapshot.State == TaskState.Terminated);
        Assert.Equal(TaskState.Terminated, stopped.State);
    }

    /// <summary>命令节点静态输入就绪即启动，执行期间保持运行。</summary>
    private const string BlockingCommandFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }],
                "From": [{ "目标": "Start@目标" }],
                "Nodes": [
                  {
                    "Name": "命令",
                    "Kind": "Command",
                    "Program": "blocked",
                    "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                    "From": [{ "触发": "@目标" }]
                  }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>不结束的进程替身：启动后等待取消，取消时报告已中断。</summary>
    private sealed class BlockingProcessRunner : IProcessRunner
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _interrupted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Task Interrupted => _interrupted.Task;

        public ProcessResult Run(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout) =>
            throw new NotSupportedException("测试进程只支持异步启动。");

        public async Task<ProcessResult> RunAsync(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _interrupted.TrySetResult();
                throw;
            }

            throw new InvalidOperationException("测试进程不应自行结束。");
        }
    }
}