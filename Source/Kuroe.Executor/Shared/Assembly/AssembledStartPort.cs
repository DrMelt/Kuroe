using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的流程开始端口：名字、类型、模式、说明、固定值与提交默认值。
/// 开始端口不是节点，根作用域的 @端口 取用它。</summary>
public sealed record AssembledStartPort(
    PortName Name,
    PortType Type,
    PortMode Mode,
    string? Description = null,
    string? Value = null,
    string? Default = null)
{
    /// <summary>是否为常量端口，值在任务内确定一次。</summary>
    public bool IsConstant => Mode == PortMode.Constant;

    /// <summary>是否携带触发值。</summary>
    public bool Trigger => Mode == PortMode.Trigger;
}