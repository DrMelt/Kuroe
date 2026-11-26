using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>端口模式：普通、触发门控或任务内常量。</summary>
public enum MafPortMode
{
    /// <summary>普通端口。</summary>
    Plain,

    /// <summary>触发端口，取值携带触发位。</summary>
    Trigger,

    /// <summary>常量端口，值在任务内确定一次。</summary>
    Constant,
}

/// <summary>触发条件：来源触发位满足或取反后满足。</summary>
public enum MafTriggerCondition
{
    /// <summary>触发位为真时放行。</summary>
    Positive,

    /// <summary>触发位为假时放行。</summary>
    Negative,
}

/// <summary>执行步骤的输出端口计划：名字、取值种类、触发标记与是否要求交回。</summary>
public sealed record MafOutputPort(
    PortName Name,
    ExecutionPortKind Kind,
    bool Trigger = false,
    bool Required = true);