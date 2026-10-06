using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>图上的一条依赖边：目标执行节点的上下文取自来源节点，来源可以是执行节点或容器，
/// 消费方式由 <see cref="Feed"/> 描述，取整份产出或来源的命名输出端口由 <see cref="FromPort"/> 指明。
/// <see cref="IsContextInput"/> 标记上下文输入边，其内容置于目标上下文开头而不是对话之后。
/// 边只汇入执行节点。</summary>
public sealed record FlowEdge(int From, int To, EdgeFeed Feed, NodeName? FromPort = null, bool IsContextInput = false);
