using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>一条装配完成的端口绑定：从已解析的来源端口取值，进入目标节点的输入端口。
/// 来源节点为空表示流程开始端口；来源端口为空表示按节点整体取值。
/// 角色决定参与方式：动态绑定进入启动组并驱动新版本，门控绑定只参与齐备与抑制，静态绑定只作为启动条件，别名只表达容器传入端口。</summary>
public sealed record AssembledInputBinding(
    AssembledPath Target,
    AssembledReference Source,
    PortName? TargetInput = null,
    PortType? Type = null,
    bool Trigger = false,
    AssembledBindingRole Role = AssembledBindingRole.Dynamic,
    int Group = 0,
    TriggerCondition Condition = TriggerCondition.Positive)
{
    /// <summary>来源端口。</summary>
    public PortName? SourcePort => Source.Port;

    /// <summary>来源是否为流程开始端口。</summary>
    public bool IsStart => Source.IsStart;

    /// <summary>是否信号语义。</summary>
    public bool Signal => Type?.Semantics == PortSemantics.Signal;

    /// <summary>是否参与新版本驱动。</summary>
    public bool Drives => Role == AssembledBindingRole.Dynamic;
}