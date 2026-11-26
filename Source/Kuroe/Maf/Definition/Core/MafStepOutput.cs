using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>一条上游产出：来源步骤、端口、文本与触发位，随步骤消息在步骤之间传递。</summary>
public sealed record MafStepOutput(MafStepId Step, PortName Port, string Text)
{
    /// <summary>本次产出是否触发。</summary>
    public bool Fires { get; init; } = true;
}