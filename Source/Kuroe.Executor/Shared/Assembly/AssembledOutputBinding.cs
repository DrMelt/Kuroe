using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的输出端口来源：来源节点与来源端口。来源节点是本节点时指向本节点输入，用于常量输出转发静态输入；
/// 来源节点是其他节点时指向该节点的输出端口。</summary>
public readonly record struct AssembledOutputBinding(AssembledPath Source, PortName Port);