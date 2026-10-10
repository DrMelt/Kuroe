using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>边把来源产出交给目标执行节点时的角色，决定内容如何放置。</summary>
public enum EdgeRole
{
    /// <summary>来源产出放入目标上下文。</summary>
    Data,

    /// <summary>来源产出置于目标上下文最前，作为请求前缀段。</summary>
    ContextInput,

    /// <summary>来源产出不进目标上下文，只作触发信号：来源发布新版本即触发目标启动。</summary>
    Trigger,
}

/// <summary>图上的一条依赖边：目标执行节点的上下文取自来源节点，来源可以是执行节点或容器，
/// 消费方式由 <see cref="Feed"/> 描述，输出端口经 <see cref="Port"/> 指明（保留端口或声明端口）。
/// 角色由 <see cref="Role"/> 描述，决定来源产出是否进入目标上下文，<see cref="Or"/> 把边归入可选启动组。
/// 边只汇入执行节点。</summary>
public sealed record FlowEdge(int From, int To, EdgeFeed Feed, PortName Port, EdgeRole Role = EdgeRole.Data, string? Or = null);
