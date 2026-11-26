using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的初始输入。</summary>
internal sealed record MafStartMessage;

/// <summary>MAF 流程的开始执行器：唯一开始点，向全部入口步骤发送空消息。</summary>
internal sealed class MafStartExecutor() : Executor<MafStartMessage>(IdValue)
{
    internal const string IdValue = "__maf_start";

    internal static MafStepId StartStepId { get; } = new(IdValue);

    [SendsMessage(typeof(MafStepMessage))]
    public override async ValueTask HandleAsync(
        MafStartMessage message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(new MafStepMessage(StartStepId, []), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}