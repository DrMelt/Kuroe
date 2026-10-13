using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Executions;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Flows;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>宿主对任务的动作：提交、批准、返工、取消、改名、回答输入节点。
/// 前置条件与回执在这里判定，状态改动经推进器落地。</summary>
public sealed class TaskService
{
    private readonly TaskRegistry _registry;
    private readonly IFlowRunner _driver;
    private readonly FlowService _flows;
    private readonly NodeModelResolver _models;

    internal TaskService(
        TaskRegistry registry,
        IFlowRunner driver,
        FlowService flows,
        NodeModelResolver models)
    {
        _registry = registry;
        _driver = driver;
        _flows = flows;
        _models = models;
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

    /// <summary>以内建对话流程提交常驻对话任务，供管理层一问一答。</summary>
    public ErrorOr<TaskSnapshot> SubmitDialogue() => SubmitFlow("对话", DialogueFlow.Build(), "对话");

    /// <summary>按流程定义提交：编译图、校验开始直连下游的模型、建任务并启动推进。</summary>
    private ErrorOr<TaskSnapshot> SubmitFlow(string goal, FlowDefinition flow, string? title)
    {
        NodeGraph graph = FlowCompiler.Compile(flow);

        // 输入节点不启动 run，不需要模型；开始直连的执行节点要求模型可解析
        foreach (FlowEdge edge in graph.Outgoing(graph.Start.Index))
        {
            if (graph[edge.Target] is not ExecutableNode { Kind: not NodeKind.Input } target)
            {
                continue;
            }

            ErrorOr<ModelName> model = _models.For(target);
            if (model.IsError)
            {
                return model.ErrorsOrEmptyList;
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

        return Result.Success;
    }

    /// <summary>回答停在等待的输入节点。nodeName 为空时回答唯一待输入节点，多个时必须指定。输入经 TaskDriver.Answer 放行下游。</summary>
    public ErrorOr<Success> Answer(TaskId id, string? nodeName, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [TaskErrors.EmptyAnswer()];
        }

        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        ErrorOr<Success> result;
        lock (task.Gate)
        {
            result = _driver.Answer(task, nodeName, input);
        }

        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        return Result.Success;
    }

    /// <summary>回答停在等待的输入节点并等待本轮产出 run 收口，返回对话回复。过程增量写给 observer。用于环内输入节点的对话任务一问一答。</summary>
    public async Task<ErrorOr<DialogueReply>> AskAsync(
        TaskId id,
        string? nodeName,
        string input,
        ITurnSink? observer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [TaskErrors.EmptyAnswer()];
        }

        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int? outputNode;
        int runBaseline;
        lock (task.Gate)
        {
            runBaseline = task.Runs.Count;
            outputNode = task.Graph.ExecutableNodes
                .Where(node => node.Kind != NodeKind.Input)
                .Select(node => node.Index)
                .FirstOrDefault();
        }

        if (outputNode is not { } target)
        {
            return [TaskErrors.NoDialogueTarget()];
        }

        // 任务刚提交时推进可能尚未把挂点评估出来，回答前等任务停在待输入
        ErrorOr<Success> waited = await WaitForAwaitingInputAsync(task, cancellationToken);
        if (waited.IsError)
        {
            return waited.ErrorsOrEmptyList;
        }

        var reply = new TaskCompletionSource<ErrorOr<DialogueReply>>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 本轮产出的 run，由挂载通知写下；未挂上时任务状态只用来判定不会再有本轮结果
        Run? round = null;

        // 订阅先于回答挂上：本轮 run 可能紧接回答就收口，晚挂会漏掉通知
        Action unsubscribeAttached = task.SubscribeRunAttached(run =>
        {
            if (run.Context.NodeIndex != target)
            {
                return;
            }

            lock (task.Gate)
            {
                if (task.Runs.TakeWhile(candidate => !ReferenceEquals(candidate, run)).Count() < runBaseline)
                {
                    return;
                }

                round = run;
            }

            if (observer is { } sink)
            {
                run.TextReceived += sink.OnText;
            }
        });
        Action unsubscribeSettled = task.SubscribeRunSettled(run =>
        {
            if (run.Context.NodeIndex != target)
            {
                return;
            }

            lock (task.Gate)
            {
                if (task.Runs.TakeWhile(candidate => !ReferenceEquals(candidate, run)).Count() < runBaseline)
                {
                    return;
                }
            }

            // 收口后不再有增量，解除观察者的流式订阅
            if (observer is { } sink)
            {
                run.TextReceived -= sink.OnText;
            }

            if (run.State == RunState.Succeeded)
            {
                reply.TrySetResult(new DialogueReply(run.Result ?? string.Empty));
                return;
            }

            IReadOnlyList<string> failures = [.. run.Snapshot().Failures];
            reply.TrySetResult(Error.Failure("Task.Ask",
                failures.Count > 0 ? string.Join("；", failures) : "本轮对话没有收口。"));
        });

        try
        {
            ErrorOr<Success> answered = Answer(id, nodeName, input);
            if (answered.IsError)
            {
                return answered.ErrorsOrEmptyList;
            }

            while (true)
            {
                if (reply.Task.IsCompleted)
                {
                    if (reply.Task.Result.IsError)
                    {
                        return await reply.Task;
                    }

                    // 回复产出已定，等反馈复位完成、任务回到等待再返回，避免下一轮回答撞上复位窗口；
                    // 任务不再推进时不会再回到等待，本轮回复仍然成立
                    TaskState after;
                    lock (task.Gate)
                    {
                        after = task.State;
                    }

                    if (after != TaskState.Running)
                    {
                        return await reply.Task;
                    }
                }
                else
                {
                    TaskState state;
                    bool started;
                    lock (task.Gate)
                    {
                        state = task.State;
                        started = round is not null;
                    }

                    // 本轮 run 已挂上时结果由它的收口给出，未挂上才按任务状态判定收口
                    if (!started && state is (TaskState.Blocked or TaskState.Canceled or TaskState.Done))
                    {
                        return [TaskErrors.DialogueSettled(id, state)];
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (task.Gate)
            {
                foreach (Run run in task.Runs.Where(run => run.IsLive))
                {
                    run.Cancel();
                }
            }

            return Error.Failure("Task.Ask", "已取消。");
        }
        finally
        {
            unsubscribeAttached();
            unsubscribeSettled();
        }
    }

    /// <summary>等任务停在待输入再回答：对话任务提交后推进循环异步把挂点评估出来，过早回答命中不了输入节点。
    /// Done 可能是挂点评估前的瞬态，真收口由 DialogueHost 重建兜底，这里只拦截阻塞与取消。</summary>
    private static async Task<ErrorOr<Success>> WaitForAwaitingInputAsync(WorkTask task, CancellationToken cancellationToken)
    {
        while (true)
        {
            TaskState state;
            lock (task.Gate)
            {
                state = task.State;
            }

            if (state == TaskState.AwaitingInput)
            {
                return Result.Success;
            }

            if (state is TaskState.Blocked or TaskState.Canceled)
            {
                return [TaskErrors.DialogueSettled(task.Id, state)];
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellationToken);
        }
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
