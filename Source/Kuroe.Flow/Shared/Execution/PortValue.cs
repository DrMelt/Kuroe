namespace Kuroe.Flow.Shared.Execution;

/// <summary>一次交回的端口产出：文本与是否触发下游。Signal 语义的文本不进入产出，Trigger 版端口按交回值给出触发位；
/// 未给出触发位时按触发处理。</summary>
public readonly record struct PortValue(string? Text, bool? Trigger = null)
{
    /// <summary>本次产出是否触发下游，未给出触发位时为 true。</summary>
    public bool Fires => Trigger ?? true;
}