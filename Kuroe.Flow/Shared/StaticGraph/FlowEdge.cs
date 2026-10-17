namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>图上的一条依赖边：目标执行节点的输入取自来源节点，来源可以是执行节点或容器。
/// <see cref="Role"/> 区分取值边与空间边，<see cref="Scope"/> 与 <see cref="Recurrence"/> 描述取值边的取值域与消费次数。
/// 来源端口经 <see cref="SourcePort"/> 指明，目标输入经 <see cref="TargetInput"/> 指明，类型取自目标输入端口的声明。
/// <see cref="Group"/> 是所属启动组，组间任一齐备即启动，组内全部齐备。边只汇入执行节点。</summary>
public sealed record FlowEdge(
    NodePath Source,
    NodePath Target,
    EdgeKind Role,
    FeedScope Scope,
    FeedRecurrence Recurrence,
    PortName SourcePort,
    PortName TargetInput,
    PortType Type,
    int Group = 0)
{
    /// <summary>提供实例空间来源的空间边，取值域与消费次数不参与判定。</summary>
    public static FlowEdge Split(NodePath source, NodePath target, PortName sourcePort, PortName targetInput, int group = 0) =>
        new(source, target, EdgeKind.Split, FeedScope.Whole, FeedRecurrence.Repeat, sourcePort, targetInput, PortType.Text, group);
}
