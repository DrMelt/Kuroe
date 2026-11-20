using Kuroe.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;
using Kuroe.Maf.Compilation.Shared;

namespace Kuroe.Maf.Compilation;

/// <summary>MAF 步骤执行器能力的注册表：按节点类型名登记，同名覆盖，装配后只读。</summary>
public sealed class MafNodeExecutions : IMafNodeExecutions
{
    private readonly Dictionary<NodeKind, Func<INodeType, IExecutionBinder?>> _entries = [];

    /// <summary>登记一种节点类型的执行器能力。</summary>
    public void Register(NodeKind kind, Func<INodeType, IExecutionBinder?> execution) =>
        _entries[kind] = execution;

    /// <inheritdoc/>
    public IExecutionBinder? Execution(INodeType type) =>
        _entries.TryGetValue(type.Kind, out Func<INodeType, IExecutionBinder?>? execution)
            ? execution(type)
            : null;
}
