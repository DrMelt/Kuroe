using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>一条常量依赖：目标执行节点的静态输入需要 Once 来源节点唯一一次执行产出该端口的文本。</summary>
public readonly record struct StaticDependency(NodePath Source, PortName Port, NodePath Target);
