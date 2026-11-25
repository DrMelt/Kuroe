namespace Kuroe.Maf.Control;

/// <summary>执行一次流程步骤，把上游消息转成下游消息。</summary>
internal delegate ValueTask<MafStepMessage> MafStepHandler(
    MafStepMessage message,
    CancellationToken cancellationToken);