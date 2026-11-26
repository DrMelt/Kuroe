using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>运行事件转换：开始执行器的成败不产生步骤事件，失败转为流程错误。</summary>
public sealed class MafWorkflowEventTranslatorTests
{
    private static readonly IReadOnlyDictionary<string, MafStep> Steps = new Dictionary<string, MafStep>();

    [Fact(DisplayName = "开始执行器事件不产生步骤事件")]
    public void Start_executor_events_do_not_produce_step_events()
    {
        Assert.Null(MafWorkflowEventTranslator.Translate(
            new ExecutorInvokedEvent(MafStartExecutor.IdValue, new object()),
            Steps,
            MafStartExecutor.IdValue));
        Assert.Null(MafWorkflowEventTranslator.Translate(
            new ExecutorCompletedEvent(MafStartExecutor.IdValue, new object()),
            Steps,
            MafStartExecutor.IdValue));
    }

    [Fact(DisplayName = "开始执行器失败转为流程错误")]
    public void Start_executor_failure_becomes_a_workflow_error()
    {
        MafWorkflowEvent? translated = MafWorkflowEventTranslator.Translate(
            new ExecutorFailedEvent(MafStartExecutor.IdValue, new InvalidOperationException("开始失败")),
            Steps,
            MafStartExecutor.IdValue);

        MafWorkflowFailed failed = Assert.IsType<MafWorkflowFailed>(translated);
        Assert.Contains("开始失败", failed.Reason, StringComparison.Ordinal);
    }
}