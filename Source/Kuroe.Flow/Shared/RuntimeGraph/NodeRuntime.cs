using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一个节点在任务内的运行期对象：节点定义与自身状态的合体，也是下游读产出的来源状态。
/// 引擎按它读放行与版本，下游按它读端口取值。状态改动的时机由引擎与节点实现共同保证在任务 Gate 下。</summary>
public abstract class NodeRuntime(GraphNode node) : ISourceState
{
    /// <summary>提交时锁定的节点定义，不可变。</summary>
    public GraphNode Node { get; } = node;

    /// <summary>节点路径，框架路由、来源读数与快照定位都以它为准。</summary>
    public NodePath Path => Node.Path;

    /// <summary>产出已放行、可被下游消费。</summary>
    public abstract bool Released { get; }

    /// <summary>产出版本：执行节点给发布号，容器给齐备代数，等待型节点给放行代数。下游以此判断来源是否有未消费的产出。</summary>
    public abstract long Revision { get; }

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>节点已声明等待外部输入，尚未交付。等待中的节点算任务活动。</summary>
    public virtual bool AwaitingInput => false;

    /// <summary>节点是否从输入端口取内容激活。输入节点等待外部交付，不依赖来源组齐备。</summary>
    public virtual bool NeedsInput => true;

    /// <summary>上一次激活使用的输入侧组序号，强制重跑按它装配输入。</summary>
    public int? LastInputGroup { get; private set; }

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();

    /// <summary>记下本次激活使用的输入侧组序号。</summary>
    public void RecordInputGroup(int group) => LastInputGroup = group;

    /// <summary>节点被激活：派发型节点由引擎按就绪条件启动执行，其余类型消费引擎装入的输入。</summary>
    public virtual void Activate(INodeContext context, NodeInputValues inputs)
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

    /// <summary>按输出端口取单段文本，端口未交回或未放行时为空。容器按绑定解析成员取值。</summary>
    public abstract string? PortText(INodeContext context, PortName port);

    /// <summary>按输出端口取产出：每条给出出处与取值文本。未放行或端口未产出时为空。</summary>
    public abstract IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port);

    /// <summary>该输出端口最近一次产出是否触发下游。没有触发值的端口恒为 true。</summary>
    public virtual bool Triggers(PortName port) => true;
}
