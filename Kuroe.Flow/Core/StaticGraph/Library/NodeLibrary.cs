using System.Diagnostics.CodeAnalysis;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.StaticGraph.Library;

/// <summary>节点库：节点定义的注册中心。同名后注册覆盖先注册，注册在装配期完成，之后只读查找。</summary>
internal sealed class NodeLibrary
{
    private readonly List<NodeName> _order = [];
    private readonly Dictionary<NodeName, NodeSpec> _byName = [];

    /// <summary>注册一条定义，同名覆盖已有定义并保留先注册的位置。</summary>
    public void Register(NodeSpec node)
    {
        if (!_byName.ContainsKey(node.Name))
        {
            _order.Add(node.Name);
        }

        _byName[node.Name] = node;
    }

    /// <summary>注册一批定义，同名覆盖已有定义，交回批内重复的名字。</summary>
    public IReadOnlyList<NodeName> Register(IReadOnlyList<NodeSpec> nodes)
    {
        HashSet<NodeName> seen = [];
        List<NodeName> duplicated = [];
        foreach (NodeSpec node in nodes)
        {
            if (!seen.Add(node.Name))
            {
                duplicated.Add(node.Name);
            }

            Register(node);
        }

        return duplicated;
    }

    /// <summary>按名取定义。</summary>
    public bool TryFind(NodeName name, [NotNullWhen(true)] out NodeSpec? definition) =>
        _byName.TryGetValue(name, out definition);

    /// <summary>按注册顺序的定义快照。</summary>
    public IReadOnlyList<NodeSpec> All() => [.. _order.Select(name => _byName[name])];

    /// <summary>复制一份，供装配在副本上叠加定义后整体替换。</summary>
    public NodeLibrary Copy()
    {
        NodeLibrary copy = new();
        foreach (NodeName name in _order)
        {
            copy.Register(_byName[name]);
        }

        return copy;
    }
}
