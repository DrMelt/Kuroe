using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Tasks;

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

    /// <summary>提交任务：按流程建任务并启动执行。</summary>
    public ErrorOr<TaskSnapshot> Submit(string goal, FlowName flowName, string? title)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return [TaskErrors.EmptyGoal()];
        }

        ErrorOr<FlowDefinition> flow = _flows.Find(flowName);
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
