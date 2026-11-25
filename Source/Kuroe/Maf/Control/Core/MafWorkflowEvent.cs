using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Control;

/// <summary>MAF 运行产生的事件。</summary>
internal abstract record MafWorkflowEvent;

/// <summary>步骤开始运行。</summary>
internal sealed record MafStepStarted(MafStep Step) : MafWorkflowEvent;

/// <summary>步骤完成运行。</summary>
internal sealed record MafStepCompleted(MafStep Step) : MafWorkflowEvent;

/// <summary>步骤运行失败。</summary>
internal sealed record MafStepFailed(MafStep Step, string Reason) : MafWorkflowEvent;

/// <summary>流程运行失败。</summary>
internal sealed record MafWorkflowFailed(string Reason) : MafWorkflowEvent;