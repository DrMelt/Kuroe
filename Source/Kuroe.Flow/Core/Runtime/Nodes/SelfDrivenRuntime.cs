using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>自驱型节点的运行期基类：按输入侧方案判定版本驱动与执行上限，激活时产出一次，成功后推进版本，失败停在阻塞。
/// 产出是唯一的文本内容，按声明为 Text 的输出端口放出。读写都要求持有任务 Gate。</summary>
public abstract class SelfDrivenRuntime(ExecutableNode executable) : NodeRuntime(executable), IInputProgress
{
    private readonly ConsumptionLedger _consumed = new();
    private string? _content;
    private string? _failure;
    private bool _failureTaken;
    private bool _retry;
    private bool _canceled;
    private long _revision;
    private int _executions;
    private int? _lastGroup;

    /// <summary>本次操作的名字，用于失败说明。</summary>
    protected abstract string Action { get; }

    /// <summary>本次操作的目标，用于失败说明。</summary>
    protected abstract string Target { get; }

    /// <inheritdoc/>
    public override bool Released => !_canceled && _revision > 0;

    /// <inheritdoc/>
    public override long Revision => _revision;

    /// <inheritdoc/>
    public override bool Canceled => _canceled;

    long IInputProgress.ConsumedRevision(NodePath source) => _consumed.Revision(source);

    int IInputProgress.Executions => _executions;

    bool IInputProgress.ForceRerun => _retry;

    /// <summary>随任务进入取消态，之后不再激活。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>取走本次激活产生的失败原因，同一原因只取一次。</summary>
    public override string? TakeFailure()
    {
        if (_failure is null || _failureTaken)
        {
            return null;
        }

        _failureTaken = true;

        return _failure;
    }

    /// <summary>清除失败并记下一次强制重试，返工入口据此重新激活节点。</summary>
    public override void Retry()
    {
        _failure = null;
        _failureTaken = false;
        _retry = true;
    }

    /// <summary>触发齐备且版本有变化时产出一次。成功推进修订号并登记消费账，失败停在阻塞等待返工。</summary>
    public override void Activate(INodeContext context)
    {
        if (_canceled || _failure is not null)
        {
            return;
        }

        if (context.Plan(Path) is not { } plan || !plan.Ready(this, context))
        {
            return;
        }

        int group = _retry && _lastGroup is { } last
            ? last
            : plan.DrivenGroup(this, context) ?? plan.AvailableGroup(context) ?? 0;
        _lastGroup = group;

        try
        {
            string? failure = Produce(context, group);
            if (failure is null)
            {
                _revision++;
                _executions++;
            }
            else
            {
                Fail(failure);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Fail($"{Action} {Target} 失败：{ex.Message}");
        }
        finally
        {
            _retry = false;
            _consumed.Record(plan, context);
        }
    }

    /// <summary>按输出端口取单段文本：只有 Text 端口承载内容，未成功产出时为空。</summary>
    public override string? PortText(INodeContext context, PortName port) =>
        Released && executable.FindPort(port) is { Type: PortType.Text } ? _content : null;

    /// <summary>按输出端口取产出：Text 端口给出本次产出的文本，出处指向本节点。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Released && executable.FindPort(port) is { Type: PortType.Text } && _content is { Length: > 0 } content
            ? [new ContextValue(new NodeSource(Path, port), content)]
            : [];

    /// <summary>节点在快照里的一刻状态：失败为阻塞，成功为完成。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state = NodeState.Pending;
        if (_canceled)
        {
            state = NodeState.Canceled;
        }
        else if (_failure is not null)
        {
            state = NodeState.Blocked;
        }
        else if (Released)
        {
            state = NodeState.Done;
        }

        return new ExecutableStateSnapshot(Path, state);
    }

    /// <summary>执行本次产出，成功时写入内容并返回空，失败时返回原因。</summary>
    protected abstract string? Produce(INodeContext context, int group);

    /// <summary>记下本次成功产出的内容。</summary>
    protected void SetContent(string content) => _content = content;

    private void Fail(string failure)
    {
        _failure = failure;
        _failureTaken = false;
    }
}