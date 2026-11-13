using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.StaticGraph.Graph;

/// <summary>一条端口绑定：从来源端口取值，进入目标节点的输入端口。来源可以是节点或流程开始端口；节点来源的端口为空表示按节点整体取值。
/// 角色决定参与方式：动态绑定进入启动组并驱动新版本，门控绑定只参与齐备与抑制，静态绑定只作为启动条件，别名只表达容器传入端口。
/// 触发门控条件决定来源触发值如何放行本绑定，普通端口恒放行。</summary>
public sealed record InputBinding(
    NodePath Target,
    BindingSource Source,
    PortName? TargetInput = null,
    PortType? Type = null,
    bool Trigger = false,
    BindingRole Role = BindingRole.Dynamic,
    int Group = 0,
    TriggerCondition Condition = TriggerCondition.Positive)
{
    /// <summary>容器成员绑定：按成员整体推进容器发布。</summary>
    public static InputBinding Member(NodePath target, NodePath member) =>
        new(target, BindingSource.ForNode(member));

    /// <summary>流程开始端口来源。</summary>
    public static InputBinding Start(
        NodePath target,
        PortName sourcePort,
        PortName? targetInput = null,
        PortType? type = null,
        bool Trigger = false,
        BindingRole Role = BindingRole.Dynamic,
        int Group = 0,
        TriggerCondition Condition = TriggerCondition.Positive) =>
        new(target, BindingSource.ForStart(sourcePort), targetInput, type, Trigger, Role, Group, Condition);

    /// <summary>来源端口。开始来源恒有端口，节点来源可以没有端口。</summary>
    public PortName? SourcePort => Source.Port;

    /// <summary>来源是否为流程开始端口。</summary>
    public bool IsStart => Source.IsStart;

    /// <summary>是否信号语义，组内有信号驱动绑定时只认信号来源的新版本。</summary>
    public bool Signal => Type?.Semantics == PortSemantics.Signal;

    /// <summary>是否参与新版本驱动。</summary>
    public bool Drives => Role == BindingRole.Dynamic;

    /// <summary>是否承载触发值，参与启动组抑制。</summary>
    public bool CarriesTrigger => Trigger;

    /// <summary>已消费版本的键。</summary>
    public ConsumptionKey Key => new(Source.Node, Source.Port);
}

/// <summary>绑定的参与方式。</summary>
public enum BindingRole
{
    /// <summary>动态驱动：进入启动组，参与齐备与版本驱动。</summary>
    Dynamic,

    /// <summary>门控：参与齐备与抑制，不驱动新版本。</summary>
    Gate,

    /// <summary>静态：只作为启动条件，不进入版本与触发。</summary>
    Static,

    /// <summary>别名：容器传入端口到外部来源的绑定，不参与容器发布。</summary>
    Alias,
}

/// <summary>输入侧的一个启动组：组内绑定全部可用时组齐备，组间任一齐备即可启动。
/// 驱动绑定按新版本启动，组内有信号驱动绑定时只认信号来源；门控与静态绑定只参与齐备与抑制。</summary>
public sealed record InputGroup(IReadOnlyList<InputBinding> Bindings)
{
    /// <summary>参与新版本驱动的绑定。</summary>
    public IEnumerable<InputBinding> DrivingBindings => Bindings.Where(binding => binding.Drives);

    /// <summary>组是否由信号驱动绑定驱动。</summary>
    public bool DrivenBySignals => DrivingBindings.Any(binding => binding.Signal);

    /// <summary>组内参与驱动的信号绑定。</summary>
    public IEnumerable<InputBinding> Signals => DrivingBindings.Where(binding => binding.Signal);
}
