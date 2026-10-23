using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Cli.Commands;

/// <summary>run 命令：提交任务并在本进程内驱动到完成、阻塞或取消。流程不能包含等待外部输入的节点。</summary>
internal sealed class RunCommand(CliSession session, CliOutput output)
{
    /// <summary>等待任务状态的轮询间隔。</summary>
    private const int PollMilliseconds = 50;

    public async Task<int> RunAsync(string flowName, string? title, string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            output.ErrorLine("用法：dotnet Kuroe.Cli.dll run --flow <流程> <目标>");

            return ExitCodes.Failure;
        }

        ErrorOr<FlowName> flow = FlowName.Create(flowName);
        if (flow.IsError)
        {
            output.Report(flow.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<CompiledFlow> compiled = session.Flows.FindCompiled(flow.Value);
        if (compiled.IsError)
        {
            output.Report(compiled.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        void report(ExecutionNotice notice) => output.Hint(notice.Text);
        session.Registry.Notified += report;
        try
        {
            ErrorOr<TaskSnapshot> submitted = session.Tasks.Submit(goal.Trim(), flow.Value, title, new ProcessSink(output));
            if (submitted.IsError)
            {
                output.Report(submitted.ErrorsOrEmptyList);

                return ExitCodes.Failure;
            }

            return await DriveAsync(submitted.Value);
        }
        finally
        {
            session.Registry.Notified -= report;
        }
    }

    /// <summary>等任务进入终态并给出退出码。</summary>
    private async Task<int> DriveAsync(TaskSnapshot task)
    {
        TaskSnapshot snapshot = await WaitAsync(task.Id);
        output.EnsureLine();
        if (snapshot.State == TaskState.Canceled)
        {
            output.ErrorLine($"任务 {task.Id} 已取消。");

            return ExitCodes.Failure;
        }

        if (snapshot.State == TaskState.Done)
        {
            output.Hint($"任务 {task.Id} 已完成。");

            return ExitCodes.Success;
        }

        PlainTaskView.Failures(output, snapshot);
        output.ErrorLine($"任务 {task.Id} 已阻塞。");

        return ExitCodes.Blocked;
    }

    /// <summary>等任务不再有在跑的执行且处于终态。</summary>
    private async Task<TaskSnapshot> WaitAsync(TaskId id)
    {
        while (true)
        {
            ErrorOr<WorkTask> found = session.Registry.Find(id);
            if (found.IsError)
            {
                throw new InvalidOperationException(string.Join('；', found.ErrorsOrEmptyList.Select(error => error.Description)));
            }

            TaskSnapshot snapshot = found.Value.Snapshot();
            if (snapshot.LiveExecutions == 0
                && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Canceled)
            {
                return snapshot;
            }

            await Task.Delay(PollMilliseconds);
        }
    }
}