using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>存档的 JSON 编解码。版本与字段映射由通用格式负责，本类只处理 JSON 文本。</summary>
public sealed class TaskRecordCodec : ITaskRecordCodec
{
    /// <inheritdoc/>
    public string FileExtension => ".json";

    /// <inheritdoc/>
    public ErrorOr<TaskRecord> Read(string text)
    {
        int version;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (ReadVersion(document.RootElement) is not { } declared)
            {
                return [Error.Failure(FlowErrorCodes.RecordFormat, "存档缺少版本。")];
            }

            version = declared;
        }
        catch (JsonException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, $"存档 JSON 不合法：{ex.Message}")];
        }

        if (!TaskRecordSerializationVersion.IsReadable(version))
        {
            return [Error.Failure(FlowErrorCodes.RecordVersion, $"不支持的存档版本 {version}。")];
        }

        TaskRecordDocument? file;
        try
        {
            file = JsonSerializer.Deserialize(text, TaskRecordJson.Default.TaskRecordDocument);
        }
        catch (JsonException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, $"存档 JSON 不合法：{ex.Message}")];
        }

        return TaskRecordSerializationMapper.FromDocument(file);
    }

    /// <inheritdoc/>
    public ErrorOr<string> Write(TaskRecord record)
    {
        ErrorOr<TaskRecordDocument> document = TaskRecordSerializationMapper.ToDocument(record);
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
