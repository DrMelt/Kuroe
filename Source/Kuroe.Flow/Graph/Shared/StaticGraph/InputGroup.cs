

namespace Kuroe.Flow.Graph.Shared.StaticGraph;

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
