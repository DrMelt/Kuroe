using System.Globalization;
using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Compiler.Description;

/// <summary>参数字段的描述读取：按类型字段表检查未知字段与值形状，再建出类型实例。</summary>
static class ParameterDocumentReader
{
    /// <summary>读出参数段。未知字段与形状不符经 fail 报出，有错误时返回空。</summary>
    public static INodeType? Read(
        INodeTypeDescription description,
        IReadOnlyDictionary<string, FlowValue> values,
        NodeScope scope,
        NodeLabel node,
        Action<Error> fail)
    {
        List<Error> errors = [];
        void Report(string message) => errors.Add(scope.Node(node.Value, message));

        foreach (string name in values.Keys.Where(name => description.Fields.All(field => field.Name != name)))
        {
            Report($"字段 {name} 不属于 {description.Kind.Value} 节点。");
        }

        INodeType type = description.Read(new Reader(values, Report), Report);

        if (errors.Count > 0)
        {
            fail(errors[0]);
            foreach (Error error in errors.Skip(1))
            {
                fail(error);
            }

            return null;
        }

        return type;
    }

    /// <summary>一轮取值的读侧：按字段名取，形状不符经 fail 报出，缺失取安全默认。</summary>
    private sealed class Reader(IReadOnlyDictionary<string, FlowValue> values, Action<string> fail) : IParameterReader
    {
        public string? Text(string name)
        {
            if (!values.TryGetValue(name, out FlowValue? value) || value is FlowValue.Null)
            {
                return null;
            }

            if (value is FlowValue.Text text)
            {
                return text.Value;
            }

            fail($"字段 {name} 必须是文本。");

            return null;
        }

        public IReadOnlyList<string> Texts(string name)
        {
            if (!values.TryGetValue(name, out FlowValue? value) || value is FlowValue.Null)
            {
                return [];
            }

            if (value is not FlowValue.Array array)
            {
                fail($"字段 {name} 必须是文本数组。");

                return [];
            }

            List<string> texts = [];
            foreach (FlowValue item in array.Items)
            {
                if (item is FlowValue.Text text)
                {
                    texts.Add(text.Value);
                }
                else
                {
                    fail($"字段 {name} 的元素必须是文本。");
                }
            }

            return texts;
        }

        public int? Integer(string name)
        {
            if (!values.TryGetValue(name, out FlowValue? value) || value is FlowValue.Null)
            {
                return null;
            }

            if (value is FlowValue.Number number
                && int.TryParse(number.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            fail($"字段 {name} 必须是整数。");

            return null;
        }
    }
}