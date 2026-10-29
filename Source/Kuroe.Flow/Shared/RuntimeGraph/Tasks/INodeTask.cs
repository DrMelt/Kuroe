using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务对节点实现与宿主扩展暴露的面：定位成员、读端口类型与在任务锁下操作节点。</summary>
public interface INodeTask
{
    /// <summary>任务的流程编译视图。</summary>
    INodeGraph Graph { get; }

    /// <summary>任务的节点上下文，宿主扩展按节点路径取节点运行期对象时使用。</summary>
    INodeContext Context { get; }

    /// <summary>在任务状态锁下执行一段操作。状态改动都要经它串行。</summary>
    T UnderLock<T>(Func<T> action);
}
