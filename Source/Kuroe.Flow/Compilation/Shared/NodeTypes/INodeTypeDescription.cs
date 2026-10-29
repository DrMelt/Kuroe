using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.NodeTypes;

/// <summary>节点类型的描述契约：类型名、静态形状、字段表与描述到类型实例的转换。
/// 描述与承载格式无关，JSON 等格式只负责把文本转成描述值。</summary>
public interface INodeTypeDescription
{
    /// <summary>形状所属的节点类型名。</summary>
    NodeKind Kind { get; }

    /// <summary>该类型的静态形状。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>参数段声明的字段与取值形态。</summary>
    IReadOnlyList<ParameterField> Fields { get; }

    /// <summary>由本轮取值建出类型实例，声明内容不合法经 fail 报出。</summary>
    INodeType Read(IParameterReader values, Action<string> fail);
}

/// <summary>节点类型描述的注册表：类型名到描述的唯一入口。</summary>
public interface INodeTypeDescriptionRegistry
{
    /// <summary>注册一个描述，同名覆盖。</summary>
    void Register(INodeTypeDescription description);

    /// <summary>按节点类型取描述，未注册时为空。</summary>
    INodeTypeDescription? Find(NodeKind kind);

    /// <summary>按文本类型名取描述，名字不区分大小写，未注册时为空。</summary>
    INodeTypeDescription? Find(string name);

    /// <summary>已注册的类型名，按注册序。</summary>
    IReadOnlyList<NodeKind> Kinds { get; }
}