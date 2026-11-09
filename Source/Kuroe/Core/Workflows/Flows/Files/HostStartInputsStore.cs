using System.Text.Json;
using ErrorOr;

namespace Kuroe.Workflows.Flows.Files;

/// <summary>hostStartInputs.json 的读取：文本形状是流程名到开始端口到宿主输入名的两层表。
/// 文件不存在时没有表项，结构不符、名字为空与重复一次给全。</summary>
sealed class HostStartInputsStore(string file)
{
    private const string Section = "HostStartInputs";

    private readonly string _file = Path.GetFullPath(file);

    /// <summary>读入输入表，文件里没有出现的流程没有表项。</summary>
    public ErrorOr<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Load()
    {
        string text;
        try
        {
            if (!File.Exists(_file))
            {
                return new Dictionary<string, IReadOnlyDictionary<string, string>>();
            }

            text = File.ReadAllText(_file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [HostStartInputsErrors.Read(_file, ex.Message)];
        }

        return Read(text);
    }

    private ErrorOr<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Read(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            return [HostStartInputsErrors.Format(_file, $"输入表不是合法 JSON：{ex.Message}")];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [HostStartInputsErrors.Format(_file, "输入表必须是 JSON 对象。")];
            }

            List<Error> errors = [];
            Dictionary<string, IReadOnlyDictionary<string, string>> forms = [];
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, Section, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(HostStartInputsErrors.Format(_file, $"输入表只写 {Section}，不认识 {property.Name}。"));
                    continue;
                }

                ReadSection(property.Value, forms, errors);
            }

            return errors.Count > 0 ? errors : forms;
        }
    }

    /// <summary>读入各流程的表项：键是开始端口名，值是宿主输入名。</summary>
    private void ReadSection(
        JsonElement section,
        Dictionary<string, IReadOnlyDictionary<string, string>> forms,
        List<Error> errors)
    {
        if (section.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (section.ValueKind != JsonValueKind.Object)
        {
            errors.Add(HostStartInputsErrors.Format(_file, $"{Section} 必须是流程名到端口表的映射。"));
            return;
        }

        foreach (JsonProperty flow in section.EnumerateObject())
        {
            string name = flow.Name.Trim();
            if (name.Length == 0)
            {
                errors.Add(HostStartInputsErrors.Format(_file, "输入表里的流程名不能为空。"));
                continue;
            }

            if (forms.ContainsKey(name))
            {
                errors.Add(HostStartInputsErrors.Format(_file, $"输入表里流程 {name} 重复。"));
                continue;
            }

            if (flow.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(HostStartInputsErrors.Format(_file, $"流程 {name} 的输入表必须是开始端口到宿主输入名的映射。"));
                continue;
            }

            Dictionary<string, string> ports = [];
            foreach (JsonProperty port in flow.Value.EnumerateObject())
            {
                string portName = port.Name.Trim();
                if (portName.Length == 0)
                {
                    errors.Add(HostStartInputsErrors.Format(_file, $"流程 {name} 的输入表里有空的开始端口名。"));
                    continue;
                }

                if (ports.ContainsKey(portName))
                {
                    errors.Add(HostStartInputsErrors.Format(_file, $"流程 {name} 的输入表里端口 {portName} 重复。"));
                    continue;
                }

                if (port.Value.ValueKind != JsonValueKind.String
                    || port.Value.GetString() is not { } input
                    || input.Trim() is not { Length: > 0 } trimmed)
                {
                    errors.Add(HostStartInputsErrors.Format(_file, $"流程 {name} 的端口 {portName} 必须给非空的宿主输入名。"));
                    continue;
                }

                ports[portName] = trimmed;
            }

            forms[name] = ports;
        }
    }
}