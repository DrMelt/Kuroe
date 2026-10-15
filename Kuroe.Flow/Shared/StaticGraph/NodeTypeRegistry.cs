namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型注册表：文件里 Kind 取值的合法性、静态形状与运行期建立的唯一来源。
/// 注册在装配前完成，之后只读。注册必须显式，不做程序集扫描。</summary>
public sealed class NodeTypeRegistry
{
    private readonly Dictionary<string, INodeType> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<INodeType> _ordered = [];

    /// <summary>注册一个类型，同名覆盖。同名注册在原位替换，类型序不变。</summary>
    public void Register(INodeType type)
    {
        int existing = _ordered.FindIndex(entry => string.Equals(entry.Type.Value, type.Type.Value, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _ordered[existing] = type;
        }
        else
        {
            _ordered.Add(type);
        }

        _byName[type.Type.Value] = type;
    }

    /// <summary>按文件里写的名字取类型，名字不区分大小写，未注册时为空。</summary>
    public INodeType? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>按类型名取类型，未注册时为空。</summary>
    public INodeType? Of(NodeTypeName type) => _byName.GetValueOrDefault(type.Value);

    /// <summary>已注册的类型名，按注册序。</summary>
    public IReadOnlyList<NodeTypeName> Types => [.. _ordered.Select(type => type.Type)];
}
