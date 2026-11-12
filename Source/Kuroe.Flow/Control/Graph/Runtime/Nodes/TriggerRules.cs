using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>输出端口的触发判定：不承载触发的端口恒触发，承载触发的端口按最近一次产出的触发位决定。</summary>
internal static class TriggerRules
{
    /// <summary>执行节点按已发布产出给出触发位；未发布或未交回该端口时按触发处理。</summary>
    public static bool Output(Port? port, PortValue? value) =>
        port is not { Trigger: true } || value is not { } actual || actual.Fires;

    /// <summary>容器输出配置触发时转发成员触发值；端口不承载触发、成员未放行或未声明来源时按触发处理。</summary>
    public static bool Container(Port? port, bool releasedMember, bool memberFires) =>
        port is not { Trigger: true } || !releasedMember || memberFires;
}
