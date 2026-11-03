using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一个节点在任务内的运行期对象：节点定义与自身状态的合体，也是下游读产出的来源状态。
/// 引擎按它读放行、版本与触发，下游按它读端口取值，两者都读最近一次发布。状态改动的时机由引擎与节点实现共同保证在任务 Gate 下。</summary>
public abstract class NodeRuntime(GraphNode node) : ISourceState, IInputProgress
{
    /// <summary>提交时锁定的节点定义，不可变。</summary>
    public GraphNode Node { get; } = node;

    /// <summary>节点路径，框架路由、来源读数与快照定位都以它为准。</summary>
    public NodePath Path => Node.Path;

    /// <summary>输入侧进展与最近一次发布。</summary>
    public InputProgress Progress { get; } = new();

    /// <summary>产出已放行、可被下游消费：有已发布产出且当前可消费。</summary>
    public bool Released => Progress.Published && Consumable;

    /// <summary>当前发布是否可消费，取消与节点自身的停驻状态在此表达。</summary>
    protected virtual bool Consumable => !Canceled;

    /// <summary>产出版本：发布时推进，作废后不回退。下游以此判断来源是否有未消费的产出。</summary>
    public long Revision => Progress.Revision;

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>节点已声明等待外部输入，尚未交付。等待中的节点算任务活动。</summary>
    public virtual bool AwaitingInput => false;

    /// <summary>节点是否从输入端口取内容激活。输入节点等待外部交付，不依赖来源组齐备。</summary>
    public virtual bool NeedsInput => true;

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();

    /// <summary>节点被激活：派发型节点由引擎按就绪条件启动执行，其余类型消费引擎装入的输入。</summary>
    public virtual void Activate(INodeContext context, INodeInputs inputs)
    {
    }

    /// <summary>取走本次激活产生的失败原因，取后不再返回同一原因。引擎在激活后调用一次。</summary>
    public virtual string? TakeFailure() => null;

    /// <summary>清除节点自身的失败，下一次激活重新尝试。派发型节点由返工作废处理，不使用它。</summary>
    public virtual void Retry()
    {
    }

    /// <summary>节点在快照里的一刻状态。</summary>
    public virtual ExecutableStateSnapshot Snapshot() => new(Path, NodeState.Pending);

    /// <summary>按输出端口取单段文本，端口没有已发布产出时为空。常量端口不进入发布，按静态解析读取。</summary>
    public string? PortText(PortName port) => Progress.Publication?.Text(port);

    /// <summary>按输出端口取已发布产出：每条给出出处与取值文本。没有已发布产出时为空。常量端口不进入发布，按静态解析读取。</summary>
    public IReadOnlyList<ContextValue> OutputValues(PortName port) =>
        Progress.Publication?.Output(port) ?? [];

    /// <summary>该输出端口在最近一次发布中是否触发下游。没有触发值的端口恒为 true。</summary>
    public bool Triggers(PortName port) => Progress.Publication?.Fires(port) ?? true;

    long IInputProgress.ConsumedRevision(ConsumptionKey key) => Progress.ConsumedRevision(key);

    void IInputProgress.Consume(InputGroup group, IReadinessSources sources) => Progress.Consume(group, sources);

    int IInputProgress.Executions => Progress.Executions;

    bool IInputProgress.Published => Progress.Published;

    bool IInputProgress.ForceRerun => Progress.ForceRerun;
}
