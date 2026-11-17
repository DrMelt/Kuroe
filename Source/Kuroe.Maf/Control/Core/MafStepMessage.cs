using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的一条上游产出，随消息在步骤之间传递。</summary>
internal sealed record MafStepOutput(MafStepId Step, PortName Port, string Text);

/// <summary>MAF 流程步骤之间传递的消息：上游步骤与它交回的端口文本。</summary>
internal sealed record MafStepMessage(MafStepId Step, IReadOnlyList<MafStepOutput> Outputs);