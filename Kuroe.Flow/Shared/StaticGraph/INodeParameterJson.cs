using System.Text.Json;
using ErrorOr;

namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型参数的文件形状：参数段的读出与写回。通用层只做编排，不认识任何参数名。</summary>
public interface INodeParameterJson
{
    /// <summary>读出参数段：values 的键是文件里未映射到结构段的字段名。缺失取安全默认，
    /// 不认识的键与形状不符的值经 fail 报出，返回的参数对象只含能读出的部分。</summary>
    INodeParameters? Read(NodeScope scope, NodeName node, IReadOnlyDictionary<string, JsonElement> values, Action<Error> fail);

    /// <summary>写回参数段：只写有值的字段。</summary>
    void Write(INodeParameters? parameters, IDictionary<string, JsonElement> target);
}