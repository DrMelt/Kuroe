using System.Text.Json;
using ErrorOr;

namespace Kuroe.Tool.Contract;

/// <summary>输出端口交回的命名段：把模型给的 JSON 对象解析成端口名到文本，不合法时给出一条可回给模型的原因。
/// 端口名与取值是否够用按声明判定，不在这一层。</summary>
internal static class PortValues
{
    public static ErrorOr<IReadOnlyDictionary<string, string>> Parse(string valuesJson)
    {
        Dictionary<string, string>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(valuesJson, PortJson.Default.DictionaryStringString);
        }
        catch (JsonException ex)
        {
            return [PortError($"不是合法 JSON：{ex.Message}")];
        }

        if (parsed is null || parsed.Count == 0)
        {
            return [PortError("端口产出为空")];
        }

        return parsed;
    }

    private static Error PortError(string reason) => Error.Validation(
        "Tool.Argument", $"{reason}。要提交端口名到文本的 JSON 对象。");
}