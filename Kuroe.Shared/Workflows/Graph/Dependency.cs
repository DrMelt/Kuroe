using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条图依赖：来源节点序号与命名的输出端口，来源可以是执行节点或容器，未带端口即取整份产出。
/// <see cref="Or"/> 把依赖归入可选启动组，组内成员产出一并取用、任一组全齐备即启动；
/// <see cref="Signal"/> 只作触发信号，来源产出不进目标上下文；<see cref="Context"/> 的来源产出置于目标上下文最前。
/// Or、Signal、Context 至多一个。</summary>
public readonly record struct Dependency(int From, PortName? Port, string? Or = null, bool Signal = false, bool Context = false);
