namespace Kuroe.Agent;

/// <summary>任务标识，按提交顺序递增。</summary>
public readonly record struct TaskId(int Value)
{
    /// <summary>任务标识的文本形式。</summary>
    public override string ToString() => $"任务 #{Value}";
}
