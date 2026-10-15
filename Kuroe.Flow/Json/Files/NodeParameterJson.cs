using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Json.Files;

/// <summary>参数段的 JSON 形状：文件里的键值转成一轮取值交给类型，类型给出的一轮取值写回键值。
/// 未知字段与形状不符在此报出，参数怎么建与语义规则由类型自己报出。</summary>
static class NodeParameterJson
{
    /// <summary>读出参数段：未知字段与形状不符经 fail 报出，返回的参数对象随错误一起丢弃。</summary>
    internal static INodeParameters? Read(
        NodeKind kind,
        NodeScope scope,
        NodeName node,
        INodeParameterFile shape,
        IReadOnlyDictionary<string, JsonElement> values,
        Action<Error> fail)
    {
        void Report(string message) => fail(scope.Node(node.Value, message));

        foreach (string name in values.Keys.Where(name => shape.Fields.All(field => field.Name != name)))
        {
            Report($"字段 {name} 不属于 {kind.Value} 节点。");
        }

        return shape.Read(new Reader(values, Report), Report);
    }

    /// <summary>写回参数段：按取值形态写出 JSON 值，类型未写的字段不写出。</summary>
    internal static void Write(
        INodeParameterFile shape,
        INodeParameters? parameters,
        IDictionary<string, JsonElement> target)
    {
        foreach (ParameterValue value in shape.Write(parameters).Values)
        {
            target[value.Name] = value.Kind == ParameterValueKind.Texts
                ? JsonSerializer.SerializeToElement([.. value.Texts], FlowJson.Default.ListString)
                : JsonSerializer.SerializeToElement(value.Text!, FlowJson.Default.String);
        }
    }

    /// <summary>一轮取值的读侧：按字段名取，形状不符经 fail 报出，缺失取安全默认。</summary>
    private sealed class Reader(IReadOnlyDictionary<string, JsonElement> values, Action<string> fail) : IParameterReader
    {
        public string? Text(string name)
        {
            if (!values.TryGetValue(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                fail($"字段 {name} 必须是文本。");

                return null;
            }

            return value.GetString();
        }

        public IReadOnlyList<string> Texts(string name)
        {
            if (!values.TryGetValue(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return [];
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                fail($"字段 {name} 必须是文本数组。");

                return [];
            }

            List<string> texts = [];
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    fail($"字段 {name} 的元素必须是文本。");

                    continue;
                }

                texts.Add(item.GetString()!);
            }

            return texts;
        }
    }
}