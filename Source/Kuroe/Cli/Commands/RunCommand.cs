using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Flow.Library;
using Kuroe.Kernel.Execution.Shared.Notice;

namespace Kuroe.Cli.Commands;

/// <summary>run 命令：提交任务并在本进程内驱动到完成、阻塞或取消。流程不能包含等待外部输入的节点。</summary>
internal sealed class RunCommand(CliSession session, CliOutput output)
{
    /// <summary>等待任务状态的轮询间隔。</summary>
    private const int PollMilliseconds = 50;

    /// <summary>图流程的实现标识，用于取图流程编译失败原因。</summary>
    private const string GraphImplementation = "graph";

    public async Task<int> RunAsync(string flowName, string? engine, string? title, IReadOnlyList<string> inputs)
    {
        ErrorOr<StartInputs> parsed = ParseInputs(inputs);
        if (parsed.IsError)
        {
            output.Report(parsed.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<FlowName> flow = FlowName.Create(flowName);
        if (flow.IsError)
        {
            output.Report(flow.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        ErrorOr<FlowTemplateView> selected = Select(flow.Value, engine);
        if (selected.IsError)
        {
            output.Report(selected.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        if (selected.Value.Implementation is not { } implementation)
        {
            output.Report([Error.Validation(FlowErrorCodes.FlowAssembly, "流程没有实现标识。")]);

            return ExitCodes.Failure;
        }

        void report(ExecutionNotice notice) => output.Hint(notice.Text);
        session.Registry.Notified += report;
        try
        {
            ErrorOr<TaskSnapshot> submitted = session.Tasks.Submit(
                parsed.Value,
                flow.Value,
                implementation,
                title,
                new ProcessObserver(output.Content, output.Entry));
            if (submitted.IsError)
            {
                FlowFailure? failure =
                    implementation.Value == GraphImplementation
                        ? session.Library.Failures.FirstOrDefault(candidate => candidate.Name == flow.Value)
                        : null;
                output.Report(failure is null ? submitted.ErrorsOrEmptyList : failure.Errors);

                return ExitCodes.Failure;
            }

            return await DriveAsync(submitted.Value);
        }
        finally
        {
            session.Registry.Notified -= report;
        }
    }

    /// <summary>按流程名与可选实现选出唯一变体。</summary>
    private ErrorOr<FlowTemplateView> Select(FlowName flow, string? engine)
    {
        IReadOnlyList<FlowTemplateView> all = session.Templates.FindAll(flow);
        if (engine is { Length: > 0 } text)
        {
            FlowImplementation implementation = new(text.Trim());
            FlowTemplateView? found = all.FirstOrDefault(
                candidate => candidate.Implementation == implementation);
            if (found is null)
            {
                return [Error.Validation(
                    FlowErrorCodes.FlowName,
                    $"流程 {flow} 没有实现 {implementation}。")];
            }

            return found;
        }

        return all.Count switch
        {
            0 => Failure(flow),
            1 => all[0],
            _ => Error.Validation(
                FlowErrorCodes.FlowName,
                $"流程 {flow} 由多个实现提供：{string.Join('、', all.Select(flow => flow.Implementation))}。用 --engine 选择。"),
        };
    }

    /// <summary>没有可提交变体时取图流程编译失败原因。</summary>
    private ErrorOr<FlowTemplateView> Failure(FlowName flow)
    {
        FlowFailure? failure = session.Library.Failures.FirstOrDefault(candidate => candidate.Name == flow);

        return failure is null ? FlowErrors.FlowNotFound(flow.Value) : [.. failure.Errors];
    }

    /// <summary>把 端口=值 形式的参数解析成开始输入表。</summary>
    private static ErrorOr<StartInputs> ParseInputs(IReadOnlyList<string> inputs)
    {
        List<StartInput> values = [];
        foreach (string input in inputs)
        {
            int separator = input.IndexOf('=');
            if (separator <= 0)
            {
                return [Error.Validation(FlowErrorCodes.TaskInput, $"输入要写成 端口=值，收到 {input}。")];
            }

            ErrorOr<PortName> port = PortName.Create(input[..separator]);
            if (port.IsError)
            {
                return port.ErrorsOrEmptyList;
            }

            values.Add(new StartInput(port.Value, input[(separator + 1)..]));
        }

        return new StartInputs(values);
    }

    /// <summary>等任务进入终态并给出退出码。</summary>
    private async Task<int> DriveAsync(TaskSnapshot task)
    {
        TaskSnapshot snapshot = await WaitAsync(task.Id);
        await session.Tasks.FlushAsync().ConfigureAwait(false);
        output.EnsureLine();
        if (snapshot.State == TaskState.Terminated)
        {
            output.ErrorLine($"任务 {task.Id} 已终止。");

            return ExitCodes.Failure;
        }

        if (snapshot.State == TaskState.Paused)
        {
            PlainTaskView.Failures(output, snapshot);
            PlainTaskView.Usage(output, snapshot);
            output.ErrorLine($"任务 {task.Id} 已暂停。");
            output.Hint("可在 Console 中恢复任务。");

            return ExitCodes.Paused;
        }

        if (snapshot.State == TaskState.Done)
        {
            output.Hint($"任务 {task.Id} 已完成。");
            PlainTaskView.Usage(output, snapshot);

            return ExitCodes.Success;
        }

        PlainTaskView.Failures(output, snapshot);
        PlainTaskView.Usage(output, snapshot);
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
                && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Terminated or TaskState.Paused)
            {
                return snapshot;
            }

            await Task.Delay(PollMilliseconds);
        }
    }
}