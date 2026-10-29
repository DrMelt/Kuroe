using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>图上的一条依赖边：目标执行节点的输入取自来源节点，来源可以是执行节点或容器，也可以取来源同一次执行透传的内容。
/// 来源端口经 <see cref="SourcePort"/> 指明，目标输入经 <see cref="TargetInput"/> 指明，类型取自目标输入端口的声明。
/// <see cref="Group"/> 是所属启动组，组间任一齐备即启动，组内全部齐备。边只汇入执行节点。</summary>
public sealed record FlowEdge(
    NodePath Source,
    NodePath Target,
    PortName SourcePort,
    PortName TargetInput,
    PortType Type,
    int Group = 0);
