using ErrorOr;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Execution.Tasks;

/// <summary>宿主对任务的动作：提交、返工、取消、改名。
/// 前置条件与回执在这里判定，状态改动经推进器落地。</summary>
public sealed class TaskService
{
    private readonly TaskRegistry _registry;
    private readonly IFlowRunner _driver;
    private readonly IFlowCatalog _flows;
    private readonly IExecutionTargetResolver _targets;

    internal TaskService(
        TaskRegistry registry,
        IFlowRunner driver,
        IFlowCatalog flows,
        IExecutionTargetResolver targets)
    {
        _registry = registry;
        _driver = driver;
        _flows = flows;
        _targets = targets;
    }

    /// <summary>提交任务：按流程建任务并启动 run。</summary>
    public ErrorOr<TaskSnapshot> Submit(string goal, FlowName? flowName, string? title)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return [TaskErrors.EmptyGoal()];
        }

        ErrorOr<FlowDefinition> flow = flowName is null ? _flows.Default() : _flows.Find(flowName.Value);
        if (flow.IsError)
        {
            return flow.ErrorsOrEmptyList;
        }

        ErrorOr<TaskSnapshot> submitted = SubmitFlow(goal.Trim(), flow.Value, title);

        return submitted.IsError ? submitted.ErrorsOrEmptyList : submitted.Value;
    }

    /// <summary>按流程定义提交：编译图、校验开始直连下游的宿主声明、建任务并启动推进。供宿主按自带流程提交。</summary>
    public ErrorOr<TaskSnapshot> SubmitFlow(string goal, FlowDefinition flow, string? title)
    {
        ErrorOr<NodeGraph> compiled = FlowCompiler.Compile(flow);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        NodeGraph graph = compiled.Value;

        // 不启动 run 的节点不需要宿主声明；开始直连的派发节点要求声明可解析
        foreach (FlowEdge edge in graph.Outgoing(graph.Start.Path))
        {
            if (graph[edge.Target] is not ExecutableNode target
                || graph.ShapeOf(target.Path) is { DispatchesRuns: false })
            {
                continue;
            }

            ErrorOr<INodeDeclaration> declaration = _targets.For(target);
            if (declaration.IsError)
            {
                return declaration.ErrorsOrEmptyList;
            }
        }

        WorkTask task = _registry.Create(goal, flow, graph, title);
        lock (task.Gate)
        {
            _driver.Start(task);
        }

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

        return Result.Success;
    }

    /// <summary>退出时取消全部任务与 run 并等待收口。</summary>
    public Task ShutdownAsync() => _driver.ShutdownAsync();
}
