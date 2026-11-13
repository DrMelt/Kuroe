using Kuroe.Flow.Tasks.Shared;
using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Tasks.Records.Shared;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>存档的 JSON 编解码。版本、字段映射与流程片段编解码由通用格式负责，本类处理 JSON 文本与旧版本迁移。</summary>
public sealed class TaskRecordCodec(TaskRecordSerializationMapper mapper) : ITaskRecordCodec
{
    /// <inheritdoc/>
    public string FileExtension => ".json";

    /// <inheritdoc/>
    public ErrorOr<TaskRecord> Read(string text)
    {
        TaskRecordDocument? file;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (ReadVersion(document.RootElement) is not { } version)
            {
                return [Error.Failure(FlowErrorCodes.RecordFormat, "存档缺少版本。")];
            }

            if (!TaskRecordSerializationVersion.IsReadable(version)
                && !TaskRecordSerializationVersion.IsMigratable(version))
            {
                return [Error.Failure(FlowErrorCodes.RecordVersion, $"不支持的存档版本 {version}。")];
            }

            file = TaskRecordSerializationVersion.IsReadable(version)
                ? document.RootElement.Deserialize(TaskRecordJson.Default.TaskRecordDocument)
                : TaskRecordLegacyFormat.Migrate(document.RootElement);
        }
        catch (JsonException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, $"存档 JSON 不合法：{ex.Message}")];
        }
        catch (FormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }

        return mapper.FromDocument(file);
    }

    /// <inheritdoc/>
    public ErrorOr<string> Write(TaskRecord record)
    {
        ErrorOr<TaskRecordDocument> document = mapper.ToDocument(record);
        if (document.IsError)
        {
            return document.ErrorsOrEmptyList;
        }

        try
        {
            return JsonSerializer.Serialize(
                document.Value,
                TaskRecordJson.WriteOptions.GetTypeInfo(typeof(TaskRecordDocument)));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, $"写出存档失败：{ex.Message}")];
        }
    }

    /// <summary>在严格映射字段之前读取版本，不可读的版本按版本不符拒绝，不误报为格式损坏。</summary>
    private static int? ReadVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(TaskRecordDocument.Version), StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out int version))
            {
                return version;
            }
        }

        return null;
    }
}
