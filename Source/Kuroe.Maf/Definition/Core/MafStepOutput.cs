using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>一条上游产出：来源步骤、端口与文本，随步骤消息在步骤之间传递。</summary>
public sealed record MafStepOutput(MafStepId Step, PortName Port, string Text);