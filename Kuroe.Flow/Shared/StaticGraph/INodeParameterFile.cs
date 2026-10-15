namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型参数的文件形状：字段表与取值到参数对象的转换。JSON 的读写由文件层的扩展承担，
/// 类型只声明有哪些字段与参数怎么建，引擎只透传参数对象。</summary>
public interface INodeParameterFile
{
    /// <summary>参数段声明的字段与取值形态，文件层按它判定未知字段。</summary>
    IReadOnlyList<ParameterField> Fields { get; }

    /// <summary>由本轮取值建出参数对象，形状与语义不合法经 fail 报出。</summary>
    INodeParameters? Read(IParameterReader values, Action<string> fail);

    /// <summary>由参数对象产出一轮取值，未写的字段不置入。</summary>
    ParameterValues Write(INodeParameters? parameters);
}