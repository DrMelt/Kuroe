using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程步骤之间传递的消息：上游步骤与它交回的端口文本。</summary>
internal sealed record MafStepMessage(MafStepId Step, IReadOnlyList<MafStepOutput> Outputs);