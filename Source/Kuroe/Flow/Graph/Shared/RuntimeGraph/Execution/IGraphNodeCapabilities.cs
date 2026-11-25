using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;

/// <summary>图流程的节点能力映射：按节点类型给出执行器绑定、输出投影与运行期工厂。
/// 能力只属于图流程，MAF 不参与。</summary>
public interface IGraphNodeCapabilities
{
    /// <summary>取执行器绑定，未注册时为空。</summary>
    IExecutionBinder? Execution(INodeType type);

    /// <summary>取输出投影，未注册时为空。</summary>
    INodeOutputProjection? Projection(INodeType type);

    /// <summary>取运行期工厂，未注册时为空。</summary>
    INodeRuntimeFactory? Runtime(INodeType type);
}
