using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>一个节点在任务内的运行时对象：节点定义与节点自身状态的合体。
/// 执行节点由推进方法改动状态，容器随成员推进经 TaskRuntime 刷新。读写都要求持有任务 Gate。</summary>
internal abstract class RuntimeNode(GraphNode node)
{
    /// <summary>提交时锁定的节点定义，不可变。</summary>
    public GraphNode Node { get; } = node;

    /// <summary>节点在图里的序号，框架路由与快照定位以它为准。</summary>
    public int Index => Node.Index;

    /// <summary>节点名。</summary>
    public NodeName Name => Node.Name;

    /// <summary>节点产出后是否停在等待批准。</summary>
    public NodeGate Gate => Node.Gate;

    /// <summary>产出已放行、可被下游消费：执行节点按自身发布与停驻，容器递归成员与子容器。</summary>
    public abstract bool Released { get; }

    /// <summary>整节点的产出版本：执行节点给发表号，容器给齐备代数。版本是消费账的版本依据。</summary>
    public abstract long Revision { get; }

    /// <summary>产出停在等待批准。</summary>
    public abstract bool Awaiting { get; }

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>按输出端口取单段文本：命名端口取声明段，上下文端口给拼合文本。
    /// 容器按绑定解析成员取值。端口未交回或成员未放行时为空。</summary>
    public abstract string? PortText(WorkTask task, PortName port);

    /// <summary>按输出端口取可注入的产出消息集：命名端口按声明段取值，逐实例给全部实例的该端口产出，
    /// ContextOutput 给装配上下文帧。item 指定时只取该实例的产出。
    /// 容器按绑定转成一条容器端口消息。</summary>
    public abstract IReadOnlyList<ContextMessage> OutputMessages(WorkTask task, PortName port, int? item);

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();
}
