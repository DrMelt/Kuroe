using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Json.Parameters;

/// <summary>参数段文件形状的注册表：按节点类型名取形状。注册在装配前完成，之后只读。注册必须显式，不做程序集扫描。</summary>
public sealed class NodeParameterFileRegistry
{
    private readonly Dictionary<string, INodeParameterFile> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册一个形状，同名覆盖。</summary>
    public void Register(INodeParameterFile shape) => _byName[shape.Kind.Value] = shape;

    /// <summary>按节点类型取形状，未注册时为空。</summary>
    public INodeParameterFile? Find(NodeKind kind) => _byName.GetValueOrDefault(kind.Value);
}