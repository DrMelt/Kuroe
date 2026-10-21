using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Json.Parameters;

/// <summary>节点类型的文件形状：类型名、静态形状、字段表与取值到类型实例的转换。每个节点类型由配套的 Json 项目实现本接口，
/// JSON 的读写由文件层承担，文本类型名到实现的对应只在这里。</summary>
public interface INodeTypeFile
{
    /// <summary>形状所属的节点类型名，注册与查找按它进行。</summary>
    NodeKind Kind { get; }

    /// <summary>该类型的静态形状，解释文件与校验声明按它进行。</summary>
    NodeTypeShape Shape { get; }

    /// <summary>参数段声明的字段与取值形态，文件层按它判定未知字段。</summary>
    IReadOnlyList<ParameterField> Fields { get; }

    /// <summary>由本轮取值建出类型实例，声明内容不合法经 fail 报出，未写的字段取默认。</summary>
    INodeType Read(IParameterReader values, Action<string> fail);

    /// <summary>由类型实例产出一轮取值，未写的字段不置入。</summary>
    ParameterValues Write(INodeType? type);
}