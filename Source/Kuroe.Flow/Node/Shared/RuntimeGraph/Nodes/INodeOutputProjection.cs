using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

/// <summary>节点把一次执行的产出投影为节点发布。带投影的节点自行决定端口内容、出处与触发，不交回通用执行契约。</summary>
public interface INodeOutputProjection
{
    /// <summary>按一次执行与它的输入构造节点发布。</summary>
    Publication Project(ExecutionId execution, GraphExecutionInput input, ExecutionOutput output);
}