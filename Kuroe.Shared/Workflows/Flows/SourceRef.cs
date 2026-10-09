namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点的一条上游接线条目：来源名与可选标记。来源可写 来源@端口 引用命名输出端口，
/// 不带端口即取整份产出。<see cref="Or"/> 把条目归入可选启动组，组内成员产出一并取用、任一组全齐备即启动；
/// <see cref="Signal"/> 只作触发信号，来源产出不进目标上下文；<see cref="Context"/> 把来源产出置于目标上下文最前，
/// 与系统指令构成稳定前缀。Or、Signal、Context 至多一个。</summary>
public sealed record SourceRef(NodeName Name, string? Or = null, bool Signal = false, bool Context = false);
