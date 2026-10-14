using System.Text.Json;
using ErrorOr;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.Execution.Tasks;

namespace Kuroe.Flows.Execution.Submissions;

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
