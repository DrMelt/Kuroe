namespace Kuroe.Shared.Agent;

/// <summary>agent 标识，跨任务全局递增，按号即可定位。</summary>
public readonly record struct RunId(int Value)
{
    /// <summary>agent 标识的文本形式。</summary>
    public override string ToString() => $"agent #{Value}";
}
