namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程图上的节点实体：执行节点或容器。统一编号、路径与门控。
/// 执行节点会派发 run 并交回产出，容器是成员的组织与汇合点。</summary>
public abstract record GraphNode(int Index, string Name, string Path, NodeGate Gate);