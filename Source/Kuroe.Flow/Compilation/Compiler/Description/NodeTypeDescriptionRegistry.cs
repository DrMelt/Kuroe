using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Compilation.Description;

/// <summary>节点类型描述的默认注册表：类型名到描述的唯一入口，同名覆盖，按注册序保留类型名。</summary>
public sealed class NodeTypeDescriptionRegistry : INodeTypeDescriptionRegistry
{
    private readonly Dictionary<string, INodeTypeDescription> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NodeKind> _ordered = [];

    /// <summary>注册一个描述，同名覆盖。同名注册在原位替换，类型序不变。</summary>
    public void Register(INodeTypeDescription description)
    {
        if (!_byName.ContainsKey(description.Kind.Value))
        {
            _ordered.Add(description.Kind);
        }

        _byName[description.Kind.Value] = description;
    }

    /// <summary>按节点类型取描述，未注册时为空。</summary>
    public INodeTypeDescription? Find(NodeKind kind) => _byName.GetValueOrDefault(kind.Value);

    /// <summary>按文本类型名取描述，名字不区分大小写，未注册时为空。</summary>
    public INodeTypeDescription? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>已注册的类型名，按注册序。</summary>
    public IReadOnlyList<NodeKind> Kinds => _ordered;
}