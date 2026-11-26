using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;
using AgentWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Maf.Control;

/// <summary>构建产物：workflow、执行器标识到步骤的映射与开始执行器标识。</summary>
internal sealed record MafWorkflowGraph(
    AgentWorkflow Workflow,
    IReadOnlyDictionary<string, MafStep> Steps,
    string StartId);

/// <summary>把流程定义构造成 MAF workflow：开始执行器接全部入口，步骤连接来自定义。</summary>
internal static class MafWorkflow
{
    internal static MafWorkflowGraph Build(
        MafFlowDefinition definition,
        Func<MafStep, MafStepNode> nodes)
    {
        Dictionary<MafStepId, MafStepNode> executors = definition.Steps
            .ToDictionary(step => step.Id, nodes);
        MafStartExecutor start = new();
        WorkflowBuilder builder = new(start);
        foreach (MafStep entry in definition.Entries())
        {
            builder.AddEdge(start, executors[entry.Id]);
        }

        foreach (MafEdge edge in definition.Edges)
        {
            builder.AddEdge(executors[edge.From], executors[edge.To]);
        }

        return new MafWorkflowGraph(
            builder.Build(),
            definition.Steps.ToDictionary(step => step.Id.Value),
            MafStartExecutor.IdValue);
    }
}