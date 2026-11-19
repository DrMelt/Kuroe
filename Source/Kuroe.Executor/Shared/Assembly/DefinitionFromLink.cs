using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>一条上游连接：目标输入端口、来源引用与触发门控条件。取用方式由目标输入端口的声明决定。</summary>
public sealed record DefinitionFromLink(
    PortName Input,
    DefinitionReference Reference,
    TriggerCondition Condition = TriggerCondition.Positive);

/// <summary>一组 From 连接：组内输入全部齐备该组才成立，组间任一组成即可启动；容器只有一组，作为成员的传入端口接线。</summary>
public sealed record DefinitionFromGroup(IReadOnlyList<DefinitionFromLink> Links);