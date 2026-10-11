using System.Text.Json.Serialization;
using Kuroe.Tools.Command.Model;

namespace Kuroe.Tools.Command.Files;

/// <summary>命令工具文件的 JSON 绑定。读法由源生成固定。</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(CommandToolFileDto))]
[JsonSerializable(typeof(CommandToolDto))]
[JsonSerializable(typeof(CommandParameterDto))]
[JsonSerializable(typeof(CommandToolTemplateItemDto))]
internal sealed partial class CommandToolJson : JsonSerializerContext;
