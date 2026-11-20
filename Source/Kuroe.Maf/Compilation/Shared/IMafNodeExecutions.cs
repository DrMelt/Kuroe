using Kuroe.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.Maf.Compilation.Shared;

/// <summary>MAF 步骤的执行器能力映射：按节点类型给出执行器绑定。</summary>
public interface IMafNodeExecutions
{
    /// <summary>取执行器绑定，未注册时为空。</summary>
    IExecutionBinder? Execution(INodeType type);
}
