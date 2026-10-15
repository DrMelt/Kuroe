namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型注册表：文件里 Kind 取值的合法性、静态形状与实例建立的唯一来源。
/// 注册在装配前完成，之后只读。注册必须显式，不做程序集扫描。实例按注册时给出的建法在首次取用时建出，之后留用。</summary>
public sealed class NodeTypeRegistry
{
    private readonly Dictionary<string, Registration> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Registration> _ordered = [];

    /// <summary>按类型名注册一个建法，同名覆盖。同名注册在原位替换，类型序不变。</summary>
    public void Register(NodeKind kind, Func<INodeType> create)
    {
        Registration registration = new(kind, create);
        int existing = _ordered.FindIndex(entry => string.Equals(entry.Kind.Value, kind.Value, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _ordered[existing] = registration;
        }
        else
        {
            _ordered.Add(registration);
        }

        _byName[kind.Value] = registration;
    }

    /// <summary>注册一个已建出的类型实例，其建法固定返回该实例。</summary>
    public void Register(INodeType type) => Register(type.Kind, () => type);

    /// <summary>按文件里写的名字取类型实例，名字不区分大小写，未注册时为空。</summary>
    public INodeType? Find(string name) => _byName.GetValueOrDefault(name)?.Instance();

    /// <summary>按类型取类型实例，未注册时为空。</summary>
    public INodeType? Create(NodeKind kind) => _byName.GetValueOrDefault(kind.Value)?.Instance();

    /// <summary>已注册的类型名，按注册序。</summary>
    public IReadOnlyList<NodeKind> Kinds => [.. _ordered.Select(entry => entry.Kind)];

    /// <summary>一条注册：类型名与建法，实例首次取用时建出并留用。</summary>
    private sealed class Registration(NodeKind kind, Func<INodeType> create)
    {
        private INodeType? _instance;

        public NodeKind Kind { get; } = kind;

        public INodeType Instance() => _instance ??= create();
    }
}
