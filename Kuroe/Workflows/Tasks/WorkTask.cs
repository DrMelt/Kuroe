using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Sessions;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务 = 一条长期会话线程：自己的对话历史、按流程推进的执行节点状态、启动的全部 run。
/// 可变成只经内部方法改动，宿主读 <see cref="Snapshot"/>。</summary>
public sealed class WorkTask
{
    private const int TitleLimit = 40;

    private readonly List<Run> _runs = [];
    private readonly Dictionary<int, PlanOutput> _splits = [];
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _portValues = [];
    private readonly SemaphoreSlim _turn = new(1, 1);
    private string _title;
    private int _dialogueTurns;
    private DateTimeOffset _lastActivityAt;
    private bool _canceled;

    internal WorkTask(TaskId id, string goal, FlowDefinition flow, NodeGraph graph, Session session, string? title)
    {
        Id = id;
        Goal = goal;
        Flow = flow;
        Graph = graph;
        Session = session;
        Journal = new TurnJournal();
        Runtime = new TaskRuntime(this);
        _lastActivityAt = DateTimeOffset.UtcNow;
        _title = string.IsNullOrWhiteSpace(title) ? Shorten(goal) : title;
    }

    /// <summary>任务标识。</summary>
    public TaskId Id { get; }

    /// <summary>提交时给出的目标。</summary>
    public string Goal { get; }

    /// <summary>提交时锁定的流程模板。</summary>
    public FlowDefinition Flow { get; }

    /// <summary>提交时锁定的流程编译视图。</summary>
    internal NodeGraph Graph { get; }

    internal Session Session { get; }

    /// <summary>前台对话的过程记录。</summary>
    internal TurnJournal Journal { get; }

    /// <summary>任务内状态改动的串行点。锁序固定为任务 Gate、注册表、派发器。</summary>
    internal Lock Gate { get; } = new();

    /// <summary>图驱动的执行状态，执行节点就地决定激活与发布。</summary>
    internal TaskRuntime Runtime { get; }

    /// <summary>任务标题，未给出时取目标首行。</summary>
    public string Title
    {
        get
        {
            lock (Gate)
            {
                return _title;
            }
        }
        internal set
        {
            lock (Gate)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _title = Shorten(value);
                }
            }
        }
    }

    /// <summary>各执行节点交回的条目拆分，键是产出执行节点序号，尚未交回时为空。仅在持有 <see cref="Gate"/> 时读写。</summary>
    public IReadOnlyDictionary<int, PlanOutput> Splits => _splits;

    /// <summary>某执行节点的条目拆分，尚未交回时为空。要求持有 <see cref="Gate"/>。</summary>
    internal PlanOutput? SplitFor(int executableIndex) => _splits.GetValueOrDefault(executableIndex);

    /// <summary>记录某执行节点的条目拆分。要求持有 <see cref="Gate"/>。</summary>
    internal void SetSplit(int executableIndex, PlanOutput output) => _splits[executableIndex] = output;

    /// <summary>某执行节点交回的命名输出端口值，尚未交回时为空。要求持有 <see cref="Gate"/>。</summary>
    internal IReadOnlyDictionary<string, string>? PortValuesFor(int executableIndex) => _portValues.GetValueOrDefault(executableIndex);

    /// <summary>记录某执行节点的输出端口值。要求持有 <see cref="Gate"/>。</summary>
    internal void SetPortValues(int executableIndex, IReadOnlyDictionary<string, string> values) => _portValues[executableIndex] = values;

    /// <summary>移除某执行节点的输出端口值，返工作废后端口随产出重交。要求持有 <see cref="Gate"/>。</summary>
    internal void ClearPortValues(int executableIndex) => _portValues.Remove(executableIndex);

    /// <summary>提交顺序排列的 run。仅在持有 <see cref="Gate"/> 时读写。</summary>
    internal IReadOnlyList<Run> Runs => _runs;

    /// <summary>任务的整体状态，由取消标记、执行节点状态与在跑的 run 汇总得出。</summary>
    public TaskState State
    {
        get
        {
            lock (Gate)
            {
                return Summarize();
            }
        }
    }
    /// <summary>要求持有 <see cref="Gate"/>。任务状态由取消标记、执行节点状态与在跑的 run 汇总得出，不单独维护。</summary>
    private TaskState Summarize()
    {
        if (_canceled)
        {
            return TaskState.Canceled;
        }

        if (_runs.Any(run => run.IsLive))
        {
            return TaskState.Running;
        }

        if (Runtime.HasAwaitingInput)
        {
            return TaskState.AwaitingInput;
        }

        if (Runtime.HasAwaiting)
        {
            return TaskState.AwaitingApproval;
        }

        if (Runtime.HasBlocked)
        {
            return TaskState.Blocked;
        }

        return Runtime.HasWork ? TaskState.Running : TaskState.Done;
    }

    /// <summary>任务被取消：在跑的 run 与执行节点状态一并终止。要求持有 <see cref="Gate"/>。</summary>
    internal void Cancel()
    {
        _canceled = true;
        foreach (Run run in _runs.Where(run => run.IsLive))
        {
            run.Cancel();
        }

        Runtime.Cancel();
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <summary>记下一个启动的 run 与它的执行回合，顺带在所属节点记账。要求持有 <see cref="Gate"/>。</summary>
    internal void Attach(Run run)
    {
        _runs.Add(run);
        Runtime.Executable(run.Context.NodeIndex).RecordRun(run.Context.ItemIndex);
        _lastActivityAt = DateTimeOffset.UtcNow;
    }

    internal void Touch() => _lastActivityAt = DateTimeOffset.UtcNow;

    /// <summary>当前任务的一轮前台对话。增量交给 observer，过程同时记进任务的记录。</summary>
    public async Task<ErrorOr<DialogueReply>> AskDialogueAsync(string input, ITurnSink? observer, CancellationToken cancellationToken)
    {
        if (!await _turn.WaitAsync(0, cancellationToken))
        {
            return [TaskErrors.Busy(Id)];
        }

        try
        {
            TurnScope scope;
            lock (Gate)
            {
                _dialogueTurns++;
                _lastActivityAt = DateTimeOffset.UtcNow;
                scope = new TurnScope
                {
                    Task = Id,
                    Run = null,
                    Output = DialogueDefaults.Output,
                    NodeName = DialogueDefaults.Name,
                    ItemIndex = null,
                    Journal = Journal,
                    Sink = TurnSinks.For(Journal, observer),
                    Tools = DialogueDefaults.Tools,
                };
            }

            ErrorOr<string> reply = await Session.AskAsync(input, scope, cancellationToken);

            return reply.IsError
                ? reply.ErrorsOrEmptyList
                : new DialogueReply(reply.Value, Session.LastTurnDiscarded);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>丢弃前台对话的上下文，回到起点。</summary>
    public void ResetDialogue() => Session.Reset();

    /// <summary>把 run 的结论作为一条用户消息写进会话历史，后续对话才可引用它。</summary>
    internal void Adopt(string text)
    {
        lock (Gate)
        {
            Session.Adopt(text);
            Journal.Append(new PromptEntry(text));
            _lastActivityAt = DateTimeOffset.UtcNow;
        }
    }
    /// <summary>当前状态的只读快照，含执行节点、执行节点状态、条目结论、run 与前台对话。</summary>
    public TaskSnapshot Snapshot()
    {
        lock (Gate)
        {
            Dictionary<RunId, RunSnapshot> runs = _runs.ToDictionary(run => run.Id, run => run.Snapshot());

            List<ExecutableSnapshot> executables =
            [
                .. Runtime.Executables.Select(node => new ExecutableSnapshot(node.Index, node.Executable,
                    [.. _runs.Where(run => run.Context.NodeIndex == node.Index).Select(run => runs[run.Id])])),
            ];

            List<ExecutableStateSnapshot> executableStates = [.. Runtime.Executables.Select(node => node.StateSnapshot())];

            return new TaskSnapshot(Id, _title, Goal, Flow, Graph, Summarize(), _dialogueTurns,
                _runs.Count(run => run.IsLive), new Dictionary<int, PlanOutput>(_splits), executables, executableStates,
                Runtime.ContainerSnapshots(), Journal.Entries, Journal.DroppedEntries, _lastActivityAt);
        }
    }

    /// <summary>取目标的首行作标题，过长时截断。</summary>
    private static string Shorten(string text)
    {
        string first = text.Trim().Split('\n')[0].Trim();

        return first.Length <= TitleLimit ? first : string.Concat(first.AsSpan(0, TitleLimit), "…");
    }
}
