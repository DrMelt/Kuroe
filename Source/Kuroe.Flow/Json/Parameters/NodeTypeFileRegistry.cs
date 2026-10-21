using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Json.Parameters;

/// <summary>节点类型文件形状的注册表：文件里的文本类型名到实现的唯一入口。注册在装配前完成，之后只读。注册必须显式，不做程序集扫描。</summary>
public sealed class NodeTypeFileRegistry
{
    private readonly Dictionary<string, INodeTypeFile> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NodeKind> _ordered = [];

    /// <summary>注册一个形状，同名覆盖。同名注册在原位替换，类型序不变。</summary>
    public void Register(INodeTypeFile declaration)
    {
        if (!_byName.ContainsKey(declaration.Kind.Value))
        {
            _ordered.Add(declaration.Kind);
        }

        _byName[declaration.Kind.Value] = declaration;
    }

    /// <summary>按节点类型取形状，未注册时为空。</summary>
    public INodeTypeFile? Find(NodeKind kind) => _byName.GetValueOrDefault(kind.Value);

    /// <summary>按文件里写的名字取形状，名字不区分大小写，未注册时为空。</summary>
    public INodeTypeFile? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>已注册的类型名，按注册序。</summary>
    public IReadOnlyList<NodeKind> Kinds => _ordered;
}