namespace Kuroe.Shared.Workflows.Flows;

/// <summary>图上的一条依赖边：目标节点的上下文取自来源节点的产出，消费方式由 <see cref="Feed"/> 描述。</summary>
public sealed record FlowEdge(int From, int To, EdgeFeed Feed);