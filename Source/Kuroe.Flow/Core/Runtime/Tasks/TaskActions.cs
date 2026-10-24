using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务动作：提交、返工、取消与改名。前置条件与回执在这里判定，状态改动经推进器落地。</summary>
/// <remarks>装配任务动作所需的注册表、推进器、执行目标解析与可选任务工作目录。任务号与任务列表的可见范围由注册表所在的任务域决定。</remarks>
public sealed class TaskActions(
    TaskRegistry registry,
    IFlowRunner driver,
    IExecutionTargetResolver targets,
    ITaskWorkspace? workspace)
{
    private readonly TaskRegistry _registry = registry;
    private readonly IFlowRunner _driver = driver;
    private readonly IExecutionTargetResolver _targets = targets;
    private readonly ITaskWorkspace? _workspace = workspace;

    /// <summary>按编译结果提交：校验开始输入与开始直连下游的宿主声明、建任务并按需建立工作目录，随后启动推进。
    /// 任务目录被占用时丢弃刚建的任务，换下一个任务号重来。observer 收到该任务全部执行的过程播发。</summary>
    public ErrorOr<TaskSnapshot> SubmitCompiled(
        StartInputs inputs,
        CompiledFlow flow,
        string? title,
        IProcessSink? observer = null)
    {
        INodeGraph graph = flow.Graph;

        if (CheckInputs(graph.Start, inputs) is { Count: > 0 } inputErrors)
        {
            return inputErrors;
        }

        // 不启动执行的节点不需要宿主声明；开始直连的派发节点要求声明可解析
        foreach (FlowEdge edge in graph.Outgoing(graph.Start.Path))
        {
            if (graph[edge.Target] is not ExecutableNode target
                || graph.ShapeOf(target.Path) is { DispatchesExecutions: false })
            {
                continue;
            }

            ErrorOr<INodeDeclaration> declaration = _targets.For(target);
            if (declaration.IsError)
            {
                return declaration.ErrorsOrEmptyList;
            }
        }

        while (true)
        {
            WorkTask task = _registry.Create(inputs, flow, title, observer);

            ErrorOr<Success> prepared = Result.Success;
            if (_workspace is { } directory)
            {
                prepared = directory.Prepare(task.Id, task.Flow.Name, task.Title);
            }

            if (prepared.IsError)
            {
                _registry.Discard(task);
                if (prepared.FirstError.Code == FlowErrorCodes.TaskWorkspaceConflict)
                {
                    continue;
                }

                return prepared.ErrorsOrEmptyList;
            }

            lock (task.Gate)
            {
                _driver.Start(task);
            }

            return task.Snapshot();
        }
    }

    /// <summary>开始输入必须逐个覆盖不带固定值的开始端口，且只落在这些端口上。</summary>
    private static List<Error> CheckInputs(StartNode start, StartInputs inputs)
    {
        List<(PortName Name, string? Text)> required =
        [
            .. start.Outputs
                .Where(port => port.Value is null)
                .Select(port => (port.Name, Text: inputs.Text(port.Name))),
        ];

        List<StartInput> supplied = [.. inputs.Values.DistinctBy(input => input.Port)];

        return
        [
            .. required.Where(entry => entry.Text is null).Select(entry => TaskErrors.MissingInput(entry.Name)),
            .. required
                .Where(entry => entry.Text is { } text && string.IsNullOrWhiteSpace(text))
                .Select(entry => TaskErrors.BlankInput(entry.Name)),
            .. inputs.Values
                .GroupBy(input => input.Port)
                .Where(group => group.Count() > 1)
                .Select(group => TaskErrors.DuplicateInput(group.Key)),
            .. supplied
                .Where(input => start.FindPort(input.Port) is null)
                .Select(input => TaskErrors.UnknownInput(input.Port)),
            .. supplied
                .Where(input => start.FindPort(input.Port) is { Value: not null })
                .Select(input => TaskErrors.ConstantInput(input.Port)),
        ];
    }

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        found.Value.Title = title;

        return Result.Success;
    }

    /// <summary>对被阻塞的执行节点再开一轮实施。</summary>
    public ErrorOr<Success> Rework(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int reworked;
        lock (task.Gate)
        {
            reworked = _driver.Rework(task);
        }

        if (reworked == 0)
        {
            return [TaskErrors.NotBlocked(id)];
        }

        return Result.Success;
    }

    /// <summary>取消一个执行。它所在的执行节点随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id)
    {
        ErrorOr<Execution> found = _registry.FindExecution(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        if (!found.Value.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(id, "取消")];
        }

        found.Value.Cancel();

        return Result.Success;
    }

    /// <summary>取消任务：在跑的执行全部取消，未走完的执行节点不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            _driver.Cancel(task);
        }

        return Result.Success;
    }

    /// <summary>退出时取消全部任务与执行并等待收口。</summary>
    public Task ShutdownAsync() => _driver.ShutdownAsync();
}