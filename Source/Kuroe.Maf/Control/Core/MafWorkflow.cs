using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>按定义构造 MAF workflow：每个步骤是一个执行桥接，连接来自定义。</summary>
internal static class MafWorkflow
{
    internal static Workflow Build(
        MafFlowDefinition definition,
        Func<MafStep, FunctionExecutor<MafStepMessage, MafStepMessage>> create)
    {
        Dictionary<MafStepId, FunctionExecutor<MafStepMessage, MafStepMessage>> executors = definition.Steps
            .ToDictionary(step => step.Id, create);

        WorkflowBuilder builder = new(executors[definition.Entry().Value.Id]);
        foreach (MafEdge edge in definition.Edges)
        {
            builder.AddEdge(executors[edge.From], executors[edge.To]);
        }

        return builder.Build();
    }
}