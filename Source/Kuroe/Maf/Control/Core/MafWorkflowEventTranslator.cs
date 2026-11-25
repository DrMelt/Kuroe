using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>把 MAF 运行事件转成流程运行事件，执行器标识通过构建时映射还原为步骤。</summary>
internal static class MafWorkflowEventTranslator
{
    /// <summary>转换一个事件，不需要处理时为空。</summary>
    internal static MafWorkflowEvent? Translate(
        WorkflowEvent update,
        IReadOnlyDictionary<string, MafStep> steps) =>
        update switch
        {
            ExecutorInvokedEvent invoked => Step(
                steps,
                invoked.ExecutorId,
                step => new MafStepStarted(step)),
            ExecutorCompletedEvent completed => Step(
                steps,
                completed.ExecutorId,
                step => new MafStepCompleted(step)),
            ExecutorFailedEvent failed => Step(
                steps,
                failed.ExecutorId,
                step => new MafStepFailed(step, failed.Data?.ToString() ?? string.Empty)),
            WorkflowErrorEvent error => new MafWorkflowFailed(
                error.Exception?.Message ?? error.Data?.ToString() ?? string.Empty),
            _ => null,
        };

    private static MafWorkflowEvent Step(
        IReadOnlyDictionary<string, MafStep> steps,
        string executor,
        Func<MafStep, MafWorkflowEvent> create) =>
        steps.TryGetValue(executor, out MafStep? step) && step is { } found
            ? create(found)
            : new MafWorkflowFailed($"MAF 执行器 {executor} 不在流程定义中。");
}