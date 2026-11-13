using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.StaticGraph.Nodes;

/// <summary>一条上游连接：目标输入端口、来源引用与触发门控条件。取用方式由目标输入端口的声明决定。</summary>
public sealed record FromLink(
    PortName Input,
    PortRef Ref,
    TriggerCondition Condition = TriggerCondition.Positive);

/// <summary>一组 From 连接：执行节点的组内输入全部齐备该组才成立，组间任一组成即可启动；
/// 容器只有一组，作为成员的传入端口接线。执行节点的组覆盖不带默认值的动态内容输入，控制输入不要求覆盖；容器的一组覆盖全部动态输入。</summary>
public sealed record FromGroup(IReadOnlyList<FromLink> Links);
