using System.Diagnostics.CodeAnalysis;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.NodeLibrary;

/// <summary>节点库：节点定义的注册中心。同名后注册覆盖先注册，注册在装配期完成，之后只读查找。
/// 定义表按注册顺序保留全部条目，名字索引只收有名条目，没有节点名的条目由校验报出。</summary>
internal sealed class NodeLibrary
{
    private readonly List<NodeSpec> _definitions = [];
    private readonly Dictionary<NodeName, NodeSpec> _byName = [];

    /// <summary>注册一条定义，同名的条目替换定义表里的旧条目并更新名字索引。</summary>
    public void Register(NodeSpec node)
    {
        if (node.Name is { } name)
        {
            int index = _definitions.FindIndex(definition => definition.Name == name);
            if (index >= 0)
            {
                _definitions[index] = node;
            }
            else
            {
                _definitions.Add(node);
            }

            _byName[name] = node;

            return;
        }

        _definitions.Add(node);
    }

    /// <summary>注册一批定义，交回批内重复的节点名。</summary>
    public IReadOnlyList<NodeName> Register(IReadOnlyList<NodeSpec> nodes)
    {
        HashSet<NodeName> seen = [];
        List<NodeName> duplicated = [];
        foreach (NodeSpec node in nodes)
        {
            if (node.Name is { } name && !seen.Add(name))
            {
                duplicated.Add(name);
            }

            Register(node);
        }

        return duplicated;
    }

    /// <summary>按节点名取定义。</summary>
    public bool TryFind(NodeName name, [NotNullWhen(true)] out NodeSpec? definition) =>
        _byName.TryGetValue(name, out definition);

    /// <summary>按注册顺序的定义快照，含没有节点名的条目。</summary>
    public IReadOnlyList<NodeSpec> All() => [.. _definitions];

    /// <summary>复制一份，供装配在副本上叠加定义后整体替换。</summary>
    public NodeLibrary Copy()
    {
        NodeLibrary copy = new();
        foreach (NodeSpec definition in _definitions)
        {
            copy.Register(definition);
        }

        return copy;
    }
}
