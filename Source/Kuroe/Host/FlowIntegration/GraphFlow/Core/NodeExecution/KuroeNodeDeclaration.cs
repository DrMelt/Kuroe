using ApiHub.Shared.Models;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;

namespace Kuroe.Host.FlowIntegration.GraphFlow.NodeExecution;

/// <summary>宿主对执行节点的声明：本节点执行时使用的模型。</summary>
public sealed record KuroeNodeDeclaration(ModelName Model) : INodeDeclaration
{
    string? INodeDeclaration.Model => Model.Value;
}
