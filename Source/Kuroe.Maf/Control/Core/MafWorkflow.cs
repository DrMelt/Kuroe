using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Maf.Control;

/// <summary>构建产物：workflow 与执行器标识到步骤的映射。</summary>
internal sealed record MafWorkflowGraph(
    AgentWorkflow Workflow,
    IReadOnlyDictionary<string, MafStep> Steps);

/// <summary>把流程定义构造成 MAF workflow：步骤来自定义，连接来自定义，执行器标识来自步骤标识。</summary>
internal static class MafWorkflow
{
    internal static MafWorkflowGraph Build(
        MafFlowDefinition definition,
        MafStepMessage entry,
        Func<MafStep, MafStepHandler> handlers)
    {
        Dictionary<MafStepId, FunctionExecutor<MafStepMessage, MafStepMessage>> executors = definition.Steps
            .ToDictionary(step => step.Id, step => Executor(step, handlers(step)));
        MafStep entryStep = definition.Find(entry.Step)
            ?? throw new InvalidOperationException($"步骤 {entry.Step.Value} 不在流程定义中。");

        WorkflowBuilder builder = new(executors[entryStep.Id]);
        foreach (MafEdge edge in definition.Edges)
        {
            builder.AddEdge(executors[edge.From], executors[edge.To]);
        }

        return new MafWorkflowGraph(
            builder.Build(),
            definition.Steps.ToDictionary(step => step.Id.Value));
    }

    /// <summary>把步骤处理器接到 MAF 执行器。</summary>
    private static FunctionExecutor<MafStepMessage, MafStepMessage> Executor(
        MafStep step,
        MafStepHandler handler) =>
        new(step.Id.Value, (message, _, cancellationToken) => handler(message, cancellationToken));
}