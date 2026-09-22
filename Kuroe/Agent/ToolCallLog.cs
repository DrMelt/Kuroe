namespace Kuroe.Agent;

/// <summary>进程内的工具调用记录，按发生顺序保存，追加时通知订阅者。</summary>
public sealed class ToolCallLog
{
    private readonly Lock _gate = new();
    private readonly List<ToolCallRecord> _records = [];

    /// <summary>追加记录后触发，在工具执行完成时。</summary>
    public event Action<ToolCallRecord>? Recorded;

    /// <summary>已发生的调用记录，按发生顺序。</summary>
    public IReadOnlyList<ToolCallRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    /// <summary>追加一条记录，由执行工具的包装调用。</summary>
    internal void Add(ToolCallRecord record)
    {
        lock (_gate)
        {
            _records.Add(record);
        }

        Recorded?.Invoke(record);
    }
}