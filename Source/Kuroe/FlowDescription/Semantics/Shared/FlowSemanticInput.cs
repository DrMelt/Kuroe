using Kuroe.FlowDescription.Definition.Shared.Ports;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>一个输入端口的来源解析：端口声明、启动组、触发条件、直接引用与取值终点。静态输入与非连接输入使用负数组号。
/// 默认值没有直接引用，<see cref="Binding"/> 为空。</summary>
public sealed record FlowSemanticInput(
    InputPort Port,
    int Group,
    TriggerCondition Condition,
    FlowBinding? Binding,
    FlowSource Source)
{
    /// <summary>是否来自动态连接。</summary>
    public bool IsLinked => Group >= 0;
}