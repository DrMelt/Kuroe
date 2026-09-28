namespace Kuroe.Shared.Workflows.Graph;

/// <summary>图上的一条依赖边：目标执行节点的上下文取自来源节点，来源可以是执行节点或容器，
/// 消费方式由 <see cref="Feed"/> 描述。边只汇入执行节点。</summary>
public sealed record FlowEdge(int From, int To, EdgeFeed Feed);