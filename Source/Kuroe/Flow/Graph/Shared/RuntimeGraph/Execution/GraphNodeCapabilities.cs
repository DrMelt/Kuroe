using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;

/// <summary>图节点能力的注册表：按节点类型名登记能力工厂，同名覆盖，装配后只读。</summary>
public sealed class GraphNodeCapabilities : IGraphNodeCapabilities
{
    private readonly Dictionary<NodeKind, Entry> _entries = [];

    /// <summary>登记一种节点类型的能力。未给出的能力按空处理。</summary>
    public void Register(
        NodeKind kind,
        Func<INodeType, IExecutionBinder?> execution,
        Func<INodeType, INodeOutputProjection?>? projection = null,
        Func<INodeType, INodeRuntimeFactory?>? runtime = null) =>
        _entries[kind] = new Entry(execution, projection, runtime);

    /// <inheritdoc/>
    public IExecutionBinder? Execution(INodeType type) =>
        _entries.TryGetValue(type.Kind, out Entry? entry) ? entry.Execution(type) : null;

    /// <inheritdoc/>
    public INodeOutputProjection? Projection(INodeType type) =>
        _entries.TryGetValue(type.Kind, out Entry? entry) ? entry.Projection(type) : null;

    /// <inheritdoc/>
    public INodeRuntimeFactory? Runtime(INodeType type) =>
        _entries.TryGetValue(type.Kind, out Entry? entry) ? entry.Runtime(type) : null;

    private sealed class Entry(
        Func<INodeType, IExecutionBinder?> execution,
        Func<INodeType, INodeOutputProjection?>? projection,
        Func<INodeType, INodeRuntimeFactory?>? runtime)
    {
        public IExecutionBinder? Execution(INodeType type) => execution(type);

        public INodeOutputProjection? Projection(INodeType type) => projection?.Invoke(type);

        public INodeRuntimeFactory? Runtime(INodeType type) => runtime?.Invoke(type);
    }
}
