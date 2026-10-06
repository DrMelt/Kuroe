using ErrorOr;
using Kuroe.Executions;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Sessions;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;
using Kuroe.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>宿主对任务的动作：提交、批准、返工、取消、采纳结论、改标题、切换当前任务。
/// 前置条件与回执在这里判定，状态改动经推进器落地。</summary>
public sealed class TaskService
{
    private readonly TaskRegistry _registry;
    private readonly IFlowRunner _driver;
    private readonly FlowService _flows;
    private readonly SessionFactory _sessions;
    private readonly NodeModelResolver _models;

    internal TaskService(
        TaskRegistry registry,
        IFlowRunner driver,
        FlowService flows,
        SessionFactory sessions,
        NodeModelResolver models)
    {
        _registry = registry;
        _driver = driver;
        _flows = flows;
        _sessions = sessions;
        _models = models;
    }

    /// <summary>提交任务：按流程建任务并启动 run。</summary>
    public ErrorOr<TaskSnapshot> Submit(string goal, string? flowName, string? title)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return [TaskErrors.EmptyGoal()];
        }

        ErrorOr<FlowDefinition> flow = string.IsNullOrWhiteSpace(flowName) ? _flows.Default() : _flows.Find(flowName);
        if (flow.IsError)
        {
            return flow.ErrorsOrEmptyList;
        }

        NodeGraph graph = FlowCompiler.Compile(flow.Value);
        ExecutableNode? root = graph.ExecutableNodes.FirstOrDefault(executable => !graph.Incoming(executable.Index).Any());
        if (root is null)
        {
            return [TaskErrors.NoRoot()];
        }

        ErrorOr<string> model = _models.For(root);
        if (model.IsError)
        {
            return model.ErrorsOrEmptyList;
        }

        WorkTask task = _registry.Create(goal.Trim(), flow.Value, graph, _sessions.NewDialogue(), title);
        lock (task.Gate)
        {
            _driver.Start(task);
        }

        _registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{task.Id} 已提交，流程 {flow.Value.Name}。"));

        return task.Snapshot();
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

    /// <summary>把前台对话切到该任务。</summary>
    public ErrorOr<Success> Focus(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        _registry.Focus(id);

        return Result.Success;
    }

    /// <summary>批准待批准的产出。runs 为空时放行该任务全部待批准的节点与容器。</summary>
    public ErrorOr<Success> Approve(TaskId id, IReadOnlyList<RunId>? runs = null)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int approved;
        lock (task.Gate)
        {
            approved = _driver.Approve(task, runs ?? []);
        }

        if (approved == 0)
        {
            return [TaskErrors.NotAwaiting(id)];
        }

        _registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{id} 已批准，继续下一步。"));

        return Result.Success;
    }

    /// <summary>对被阻塞的单元再开一轮实施。itemIndex 为空时处理该任务全部被阻塞的单元。</summary>
    public ErrorOr<Success> Rework(TaskId id, int? itemIndex)
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
            reworked = _driver.Rework(task, itemIndex);
        }

        if (reworked == 0)
        {
            return [TaskErrors.NotBlocked(id)];
        }

        _registry.Report(new ExecutionNotice(NoticeLevel.Info, $"{id} 已返工。"));

        return Result.Success;
    }

    /// <summary>取消一个 run。它所在的单元随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopRun(RunId id)
    {
        ErrorOr<Run> found = _registry.FindRun(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        if (!found.Value.IsLive)
        {
            return [RunErrors.RunSettled(id, "取消")];
        }

        found.Value.Cancel();

        return Result.Success;
    }

    /// <summary>取消任务：在跑的 run 全部取消，未走完的单元不再推进。</summary>
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

        _registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{id} 已取消。"));

        return Result.Success;
    }

    /// <summary>把 run 的结论作为一条消息写进任务历史，后续对话才用得上它。</summary>
    public ErrorOr<Success> Adopt(RunId id)
    {
        ErrorOr<Run> found = _registry.FindRun(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        RunSnapshot snapshot = found.Value.Snapshot();
        if (snapshot.State != RunState.Succeeded || snapshot.Result is not { Length: > 0 } result)
        {
            return [RunErrors.NothingToAdopt(id)];
        }

        ErrorOr<WorkTask> task = _registry.Find(snapshot.Context.Task);
        if (task.IsError)
        {
            return task.ErrorsOrEmptyList;
        }

        task.Value.Adopt($"{id} 的结论（{snapshot.Context.Label}）：\n{result}");

        return Result.Success;
    }

    /// <summary>退出时取消全部任务与 run 并等待收口。</summary>
    public Task ShutdownAsync() => _driver.ShutdownAsync();
}
