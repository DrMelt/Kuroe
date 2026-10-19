using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

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

    /// <summary>产出版本：执行节点给发表号，容器给齐备代数，等待型节点给放行代数。版本是消费账的依据。</summary>
    public abstract long Revision { get; }

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();

    /// <summary>节点被激活：派发型节点由引擎按就绪条件启动执行，其余类型由节点自行处理。</summary>
    public virtual void Activate(INodeContext context)
    {
    }

    /// <summary>节点在快照里的一刻状态。</summary>
    public virtual ExecutableStateSnapshot Snapshot() => new(Path, NodeState.Pending);

    /// <summary>按输出端口取单段文本，端口未交回或未放行时为空。容器按绑定解析成员取值。</summary>
    public abstract string? PortText(INodeTask task, PortName port);

    /// <summary>按输出端口取产出：每条给出出处与取值文本。未放行或端口未产出时为空。</summary>
    public abstract IReadOnlyList<ContextValue> OutputValues(INodeTask task, PortName port);
}
