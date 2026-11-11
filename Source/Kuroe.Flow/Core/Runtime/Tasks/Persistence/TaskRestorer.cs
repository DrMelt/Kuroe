using ErrorOr;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从存档恢复任务对象：重建执行历史与节点状态，不启动推进循环。</summary>
public sealed class TaskRestorer(TaskRegistry registry, IExecutionTargetResolver? targets = null) : ITaskRestorer
{
    private readonly TaskRegistry _registry = registry;
    private readonly IExecutionTargetResolver? _targets = targets;

    /// <inheritdoc/>
    public ErrorOr<WorkTask> Restore(TaskRecord record, CompiledFlow flow)
    {
        try
        {
            PortTexts startTexts = new(record.Start.StartTexts);
            List<Execution> restored = [];
            foreach (ExecutionRecord saved in record.Executions)
            {
                if (flow.Graph[saved.Node] is not ExecutableNode node)
                {
                    return [Error.Failure(
                        FlowErrorCodes.RecordGraphMismatch,
                        $"存档执行节点 {saved.Node} 不在流程图中。")];
                }

                restored.Add(Execution.Restore(saved, BuildInput(record.Identity.Id, node, saved, flow.Graph)));
            }

            WorkTask task = WorkTask.Restore(_registry, record, flow, startTexts, restored);
            foreach (Execution execution in task.Executions)
            {
                _registry.RestoreExecution(task, execution);
            }

            return task;
        }
        catch (ArgumentException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordNodeMismatch, ex.Message)];
        }
    }

    private ExecutionInput BuildInput(TaskId task, ExecutableNode node, ExecutionRecord saved, NodeGraph graph)
    {
        ExecutionInput input = new()
        {
            Task = task,
            Node = node.Path,
            Type = node.Type,
            ExecutionCount = saved.ExecutionCount,
            Inputs =
            [
                .. saved.Inputs.Select(value => new InputValue(
                    value.Port,
                    new ContextValue(
                        StaticInputSources.Of(graph, node.Path, value.Port) ?? RecordSources.Read(value.Origin),
                        value.Text))),
            ],
            Texts = saved.Texts,
            OutputPorts = node.SubmittablePorts,
        };

        if (_targets is not null && _targets.For(node, input) is { IsError: false } declaration)
        {
            input = input with { Declaration = declaration.Value };
        }

        return input;
    }
}
