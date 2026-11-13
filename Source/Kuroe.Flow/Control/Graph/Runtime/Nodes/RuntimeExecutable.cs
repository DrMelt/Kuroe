using Kuroe.Flow.Execution.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;

using Kuroe.Flow.Node.Shared.Runtime.Nodes;
using Kuroe.Flow.Tasks.Shared;
namespace Kuroe.Flow.Control.Graph.Runtime.Nodes;

/// <summary>一个执行节点在任务内的运行时对象：执行节点定义与节点自身状态的合体，交回与产出也记在它上面。
/// 待收口的交回按执行记，发布后只保留已发布那次，产出锚指向已发布的那次执行。
/// 节点状态只经推进方法改动，读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeExecutable(ExecutableNode executable) : RuntimeNode(executable)
{
    /// <summary>提交时锁定的执行节点定义，不可变。</summary>
    public ExecutableNode Executable { get; } = executable;

    /// <summary>停在本节点时记下的重新执行目标，全部处理后解除阻塞。</summary>
    private readonly List<NodePath> _rerunTargets = [];

    private bool _canceled;

    /// <summary>在跑的执行数。推进时经 AddActive 与 ReleaseActive 改动。</summary>
    public int Active { get; private set; }

    /// <summary>是否仍有在跑的执行。</summary>
    public bool HasActive => Active > 0;

    /// <summary>已启动的执行数。</summary>
    public int Executions => Progress.Executions;

    /// <summary>停在阻塞时的原因，未阻塞时为空。</summary>
    public NodeBlock? Block { get; private set; }

    /// <summary>当前阻塞是否已有可派发的恢复计划。</summary>
    public bool HasRestorePlan { get; set; }

    /// <summary>停在本节点时记下的重新执行目标，全部处理后解除阻塞。</summary>
    public IReadOnlyList<NodePath> RerunTargets => _rerunTargets;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>产出可消费：不在阻塞与取消。</summary>
    protected override bool Consumable => Block is null && !Canceled;

    /// <summary>随任务进入取消态，解除当前阻塞与重跑目标。</summary>
    public override void Cancel()
    {
        _canceled = true;
        ClearBlock();
    }

    /// <summary>记下重跑请求，供重新执行作废后的重启判定使用。</summary>
    public void RequestRerun() => Progress.RequestRerun();

    /// <summary>重跑请求已消耗。</summary>
    public void ClearRerun() => Progress.ClearRerun();

    /// <summary>本轮该交回但尚未交回的命名端口，按节点声明的需交回端口与执行契约判定。</summary>
    public IReadOnlyList<PortName> OutstandingPorts(GraphExecution execution)
    {
        IReadOnlyDictionary<PortName, PortValue>? values = execution.Contract.Ports;

        return [.. Executable.RequiredPorts
            .Select(port => port.Name)
            .Where(name => values?.ContainsKey(name) != true)];
    }

    /// <summary>置为阻塞并合并记下重新执行目标，重复目标不重复收集。已取消的节点不接受阻塞。</summary>
    public void EnterBlocked(IReadOnlyList<NodePath> targets, NodeBlock block)
    {
        if (_canceled)
        {
            return;
        }

        Block = block;
        foreach (NodePath target in targets.Where(target => !_rerunTargets.Contains(target)))
        {
            _rerunTargets.Add(target);
        }
    }

    /// <summary>移除本次重新执行处理的目标，全部处理后解除阻塞。</summary>
    public void FinishRerun(IReadOnlyList<NodePath> handled)
    {
        foreach (NodePath target in handled)
        {
            _rerunTargets.Remove(target);
        }

        if (_rerunTargets.Count == 0)
        {
            Block = null;
        }
    }

    /// <summary>解除阻塞并清空重新执行目标，恢复开始与任务取消时使用。</summary>
    public void ClearBlock()
    {
        _rerunTargets.Clear();
        Block = null;
    }

    /// <summary>当前阻塞不再关联可恢复的执行，阻塞原因与时刻保留。</summary>
    public void InvalidateRestore()
    {
        if (Block is { } block)
        {
            Block = block with { Execution = null };
        }
    }

    /// <summary>按端口产出发布，没有内容产出时使用。</summary>
    public void Publish(GraphExecution execution) => Publish(execution, ExecutionOutput.FromPorts([]));

    /// <summary>产出发布：按执行结果的形态选择端口发布或内容发布，版本单调递增，下游据此重新评估。</summary>
    public void Publish(GraphExecution execution, ExecutionOutput output)
    {
        switch (output.Kind)
        {
            case ExecutionOutputKind.Ports:
                PublishPorts(execution);
                break;

            case ExecutionOutputKind.Content:
                PublishContent(execution, output.Content);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(output), output.Kind, "未知的执行产出形态。");
        }
    }

    /// <summary>端口产出的发布：按这次执行的契约构造发布。</summary>
    private void PublishPorts(GraphExecution execution) =>
        Progress.Publish(CreatePublication(execution, Progress.NextRevision()));

    /// <summary>内容产出的发布：把同一条文本写入承载文本的输出端口，触发位按默认处理。</summary>
    private void PublishContent(GraphExecution execution, string content) =>
        Progress.Publish(CreateContentPublication(execution, content, Progress.NextRevision()));

    /// <summary>作废已发布产出，重新执行起点用它让下游重新等待。阻塞与重新执行目标不在此清除：达到执行上限的目标长期留驻，由 <see cref="FinishRerun"/> 按处理进度解除。</summary>
    public void Invalidate() => Progress.Invalidate();

    /// <summary>记一轮派发。调用点唯一：WorkTask.Attach。</summary>
    public void RecordExecution() => Progress.Started();

    /// <summary>启动即登记方案内全部取值来源的当前版本，防止同版本重复触发。</summary>
    public void Consume(NodeInputPlan plan, IReadinessSources sources)
    {
        foreach (InputGroup group in plan.Groups)
        {
            Progress.Consume(group, sources);
        }
    }

    /// <summary>一个已启动的执行终结，在跑数减一。</summary>
    public void ReleaseActive()
    {
        if (Active > 0)
        {
            Active--;
        }
    }

    /// <summary>记下新启动的执行。</summary>
    public void AddActive() => Active++;

    /// <summary>按内容产出构造一次发布：承载文本的输出端口给出同一条内容，出处指向本次执行。内容为空时只发布版本与触发。</summary>
    private Publication CreateContentPublication(GraphExecution execution, string content, long revision)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> values = content.Length > 0
            ? Executable.Outputs
                .Where(port => PortRules.CarriesText(port.Type))
                .ToDictionary(
                    port => port.Name,
                    port => (IReadOnlyList<ContextValue>)[new ContextValue(new ExecutionSource(execution.Id, Path, port.Name), content)])
            : [];

        return new Publication(revision, new Dictionary<PortName, bool>(), values);
    }

    /// <summary>按这次执行构造一次发布：承载触发的端口取交回触发位，动态 Context 端口转发来源输入内容，
    /// 承载内容的端口取交回文本，其余端口不承载内容。</summary>
    private Publication CreatePublication(GraphExecution execution, long revision)
    {
        Dictionary<PortName, bool> triggers = [];
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = [];
        foreach (Port port in Executable.Outputs)
        {
            PortValue? submitted = SubmittedValue(execution, port.Name);
            triggers[port.Name] = TriggerRules.Output(port, submitted);
            if (!port.IsConstant && port.Type.Semantics == PortSemantics.Context)
            {
                content[port.Name] = ContextInputs(execution);
                continue;
            }

            if (PortRules.CarriesContent(port.Type)
                && submitted is { Text: { Length: > 0 } text })
            {
                content[port.Name] = [new ContextValue(new ExecutionSource(execution.Id, Path, port.Name), text)];
            }
        }

        return new Publication(revision, triggers, content);
    }

    /// <summary>来源执行输入侧取到的内容，按原出处给出。</summary>
    private static IReadOnlyList<ContextValue> ContextInputs(GraphExecution execution) =>
        [.. execution.Input.Inputs.Select(value => value.Value)];

    /// <summary>按端口取这次执行交回的值，未交回时为空。</summary>
    private static PortValue? SubmittedValue(GraphExecution execution, PortName port) =>
        execution.Contract.Ports is { } values
        && values.TryGetValue(port, out PortValue value)
            ? value
            : null;

    /// <summary>捕获执行节点的进展、发布与阻塞状态。</summary>
    public override NodeRecord? CaptureRecord() =>
        new ExecutableNodeRecord(
            Path,
            CaptureProgress(),
            [.. _rerunTargets],
            Block);

    /// <summary>从存档恢复执行节点的进展、发布与阻塞状态。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
        if (record is not ExecutableNodeRecord state)
        {
            throw new ArgumentException($"执行节点 {Path} 收到 {record.GetType().Name} 记录。", nameof(record));
        }

        RestoreProgress(state.Common);
        _rerunTargets.Clear();
        _rerunTargets.AddRange(state.RerunTargets);
        Block = state.Block;
        HasRestorePlan = false;
    }

    /// <summary>执行节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state;
        if (Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Block is not null)
        {
            state = NodeState.Blocked;
        }
        else if (Active > 0)
        {
            state = NodeState.Running;
        }
        else
        {
            state = Progress.Published ? NodeState.Done : NodeState.Pending;
        }

        return new ExecutableStateSnapshot(Path, state, Block, HasRestorePlan);
    }
}