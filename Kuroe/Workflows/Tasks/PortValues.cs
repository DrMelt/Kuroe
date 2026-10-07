using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Workflows;

namespace Kuroe.Workflows.Tasks;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象解析成端口名到文本，不合法时给出一条可回给模型的原因。</summary>
static class PortValues
{
    public static ErrorOr<IReadOnlyDictionary<PortName, string>> Parse(string valuesJson)
    {
        Dictionary<string, string>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(valuesJson, PortJson.Default.DictionaryStringString);
        }
        catch (JsonException ex)
        {
            return [TaskErrors.Ports($"不是合法 JSON：{ex.Message}")];
        }

        if (parsed is null || parsed.Count == 0)
        {
            return [TaskErrors.Ports("端口产出为空")];
        }

        Dictionary<PortName, string> result = [];
        foreach ((string port, string text) in parsed)
        {
            ErrorOr<PortName> parsedPort = PortName.Create(port);
            if (parsedPort.IsError)
            {
                return parsedPort.ErrorsOrEmptyList;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return [TaskErrors.Ports($"端口 {parsedPort.Value} 或它的产出为空")];
            }

            result[parsedPort.Value] = text;
        }

        return result;
    }
}

/// <summary>输出端口交回的 JSON 绑定。读法由源生成固定，不经反射式序列化。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PortJson : JsonSerializerContext;
