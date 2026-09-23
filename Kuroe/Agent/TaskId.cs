namespace Kuroe.Agent;

/// <summary>任务标识，按提交顺序递增。</summary>
public readonly record struct TaskId(int Value)
{
    public override string ToString() => $"任务 #{Value}";
}
