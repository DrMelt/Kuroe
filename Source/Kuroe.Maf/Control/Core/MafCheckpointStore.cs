using System.Text.Json;
using Kuroe.Control.Shared;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的检查点存储：检查点随流程状态保存，恢复时回填同一份数据。</summary>
internal sealed class MafCheckpointStore : ICheckpointStore<JsonElement>
{
    private readonly Dictionary<string, Entry> _values = [];
    private readonly Dictionary<string, List<CheckpointInfo>> _index = [];
    private readonly ITaskFlowContext _task;
    private readonly MafFlowState _state;

    internal MafCheckpointStore(ITaskFlowContext task, MafFlowState state)
    {
        _task = task;
        _state = state;
        Manager = CheckpointManager.CreateJson(this, new JsonSerializerOptions(JsonSerializerDefaults.General));

        if (state.Checkpoint is not { } checkpoint)
        {
            return;
        }

        JsonElement value = JsonDocument.Parse(checkpoint.Data).RootElement.Clone();
        _values[checkpoint.Id] = new Entry(checkpoint.Session, value, null);
        _index[checkpoint.Session] = [new CheckpointInfo(checkpoint.Session, checkpoint.Id)];
    }

    /// <summary>检查点管理器。</summary>
    internal CheckpointManager Manager { get; }

    /// <inheritdoc/>
    public ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId,
        JsonElement value,
        CheckpointInfo? parent = null)
    {
        CheckpointInfo info = new(sessionId, Guid.NewGuid().ToString("N"));
        lock (_task.Gate)
        {
            _values[info.CheckpointId] = new Entry(sessionId, value, parent);
            if (!_index.TryGetValue(sessionId, out List<CheckpointInfo>? checkpoints))
            {
                checkpoints = [];
                _index[sessionId] = checkpoints;
            }

            checkpoints.Add(info);
            if (!_state.FreezeCheckpoint)
            {
                _state.Checkpoint = new MafCheckpoint(sessionId, info.CheckpointId, value.GetRawText());
            }
        }

        return new ValueTask<CheckpointInfo>(info);
    }

    /// <inheritdoc/>
    public ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        lock (_task.Gate)
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
        lock (_task.Gate)
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