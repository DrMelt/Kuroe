using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Json.Parameters;

/// <summary>容器的文件形状：类型实例由引擎提供，不接受任何类型参数。</summary>
internal sealed class ContainerTypeFile : INodeTypeFile
{
    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => ContainerNodeTypes.Name;

    /// <summary>容器的静态形状。</summary>
    public NodeTypeShape Shape => ContainerNodeTypes.Shape;

    /// <summary>容器不声明类型参数。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [];

    /// <summary>容器的类型实例，参数段没有内容。</summary>
    public INodeType Read(IParameterReader values, Action<string> fail) => ContainerNodeTypes.Type;

    /// <summary>容器不写出参数段。</summary>
    public ParameterValues Write(INodeType? type) => new();
}