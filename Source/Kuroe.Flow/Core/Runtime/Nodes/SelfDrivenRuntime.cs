using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>自驱型节点的运行期基类：激活时消费引擎装入的输入并产出一次，成功后发布，失败停在阻塞等待返工。
/// 产出是唯一的文本内容，按声明为 Text 的输出端口放出。读写都要求持有任务 Gate。</summary>
public abstract class SelfDrivenRuntime(ExecutableNode executable) : RuntimeNode(executable)
{
    private string? _content;
    private string? _failure;
    private bool _failureTaken;
    private bool _canceled;

    /// <summary>本次操作的名字，用于失败说明。</summary>
    protected abstract string Action { get; }

    /// <summary>本次操作的目标，用于失败说明。</summary>
    protected abstract string Target { get; }

    /// <summary>产出可消费：未被取消。</summary>
    protected override bool Consumable => !_canceled;

    /// <inheritdoc/>
    public override bool Canceled => _canceled;

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
        Progress.RequestRerun();
    }

    /// <summary>消费本次输入并产出一次。成功发布一次，失败停在阻塞等待返工。</summary>
    public override void Activate(INodeContext context, NodeInputValues inputs)
    {
        if (_canceled || _failure is not null)
        {
            return;
        }

        try
        {
            string? failure = Produce(inputs);
            if (failure is null)
            {
                Progress.Started();
                Progress.Publish(CreatePublication(Progress.NextRevision()));
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
            Progress.ClearRerun();
        }
    }

    /// <summary>捕获自驱节点的进展、发布与失败。</summary>
    public override NodeCheckpoint? CaptureCheckpoint() =>
        new SelfDrivenNodeCheckpoint(
            Path,
            CaptureProgress(),
            _failure,
            _failureTaken);

    /// <summary>从检查点恢复自驱节点的进展、发布与失败。</summary>
    public override void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context)
    {
        if (checkpoint is not SelfDrivenNodeCheckpoint selfDriven)
        {
            throw new ArgumentException($"自驱节点 {Path} 收到 {checkpoint.GetType().Name} 检查点。", nameof(checkpoint));
        }

        RestoreProgress(selfDriven.Common);
        _failure = selfDriven.Failure;
        _failureTaken = selfDriven.FailureTaken;
    }

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
        else if (Progress.Published)
        {
            state = NodeState.Done;
        }

        return new ExecutableStateSnapshot(Path, state);
    }

    /// <summary>执行本次产出，成功时写入内容并返回空，失败时返回原因。</summary>
    protected abstract string? Produce(NodeInputValues inputs);

    /// <summary>记下本次成功产出的内容。</summary>
    protected void SetContent(string content) => _content = content;

    /// <summary>按已产出内容构造一次发布：Text 语义端口给出同一条内容，出处指向本节点。</summary>
    private Publication CreatePublication(long revision)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = _content is { Length: > 0 } text
            ? executable.Outputs
                .Where(port => PortRules.CarriesText(port.Type))
                .ToDictionary(
                    port => port.Name,
                    port => (IReadOnlyList<ContextValue>)[new ContextValue(new NodeSource(Path, port.Name), text)])
            : [];

        return new Publication(revision, new Dictionary<PortName, bool>(), content);
    }

    private void Fail(string failure)
    {
        _failure = failure;
        _failureTaken = false;
    }
}