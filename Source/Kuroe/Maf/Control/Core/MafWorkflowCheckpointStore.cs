using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace Kuroe.Maf.Control;

/// <summary>MAF 运行期检查点存储：索引留在内存，检查点写入流程检查点契约。</summary>
internal sealed class MafWorkflowCheckpointStore : ICheckpointStore<JsonElement>
{
    private readonly Dictionary<string, Entry> _values = [];
    private readonly Dictionary<string, List<CheckpointInfo>> _index = [];
    private readonly IMafWorkflowCheckpoints _checkpoints;
    private readonly ICheckpointIdSource _ids;
    private readonly Lock _sync = new();

    internal MafWorkflowCheckpointStore(IMafWorkflowCheckpoints checkpoints, ICheckpointIdSource ids)
    {
        _checkpoints = checkpoints;
        _ids = ids;
        Manager = CheckpointManager.CreateJson(this, new JsonSerializerOptions(JsonSerializerDefaults.General));

        if (checkpoints.Load() is not { } checkpoint)
        {
            return;
        }

        Current = checkpoint;
        JsonElement value = JsonDocument.Parse(checkpoint.Data).RootElement.Clone();
        _values[checkpoint.Id] = new Entry(checkpoint.Session, value, null);
        _index[checkpoint.Session] = [new CheckpointInfo(checkpoint.Session, checkpoint.Id)];
    }

    /// <summary>检查点管理器。</summary>
    internal CheckpointManager Manager { get; }

    /// <summary>启动时读取到的检查点，没有时为空。</summary>
    internal MafWorkflowCheckpoint? Current { get; }

    /// <inheritdoc/>
    public ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId,
        JsonElement value,
        CheckpointInfo? parent = null)
    {
        CheckpointInfo info = new(sessionId, _ids.Next());
        lock (_sync)
        {
            _values[info.CheckpointId] = new Entry(sessionId, value, parent);
            if (!_index.TryGetValue(sessionId, out List<CheckpointInfo>? checkpoints))
            {
                checkpoints = [];
                _index[sessionId] = checkpoints;
            }

            checkpoints.Add(info);
        }

        _checkpoints.Save(new MafWorkflowCheckpoint(sessionId, info.CheckpointId, value.GetRawText()));

        return new ValueTask<CheckpointInfo>(info);
    }

    /// <inheritdoc/>
    public ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        lock (_sync)
        {
            return _values.TryGetValue(key.CheckpointId, out Entry entry) && entry.Session == sessionId
                ? new ValueTask<JsonElement>(entry.Value)
                : throw new KeyNotFoundException($"找不到会话 {sessionId} 的检查点 {key.CheckpointId}。");
        }
    }

    /// <inheritdoc/>
    public ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId,
        CheckpointInfo? withParent = null)
    {
        lock (_sync)
        {
            IEnumerable<CheckpointInfo> checkpoints = _index.GetValueOrDefault(sessionId) ?? [];
            if (withParent is { } parent)
            {
                checkpoints = checkpoints.Where(info =>
                    _values.TryGetValue(info.CheckpointId, out Entry entry)
                    && entry.Parent is { } actual
                    && actual.CheckpointId == parent.CheckpointId);
            }

            return new ValueTask<IEnumerable<CheckpointInfo>>([.. checkpoints]);
        }
    }

    private readonly record struct Entry(string Session, JsonElement Value, CheckpointInfo? Parent);
}