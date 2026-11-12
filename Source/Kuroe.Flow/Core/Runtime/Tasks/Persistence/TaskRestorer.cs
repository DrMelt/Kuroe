using ErrorOr;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Tasks.Records;

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
            if (record.Flow.State is not GraphFlowStateRecord state)
            {
                return [Error.Failure(FlowErrorCodes.RecordFormat, "存档的流程状态不是图流程。")];
            }

            PortTexts startTexts = new(state.Start.StartTexts);
            List<GraphExecution> restored = [];
            foreach (ExecutionRecord saved in record.Executions)
            {
                NodePath path = GraphRecordTargets.Parse(saved.Target);
                if (flow.Graph[path] is not ExecutableNode node)
                {
                    return [Error.Failure(
                        FlowErrorCodes.RecordGraphMismatch,
                        $"存档执行节点 {path} 不在流程图中。")];
                }

                restored.Add(GraphExecution.Restore(saved, BuildInput(record.Identity.Id, node, saved)));
            }

            WorkTask task = WorkTask.Restore(_registry, record, state, flow, startTexts, restored);
            foreach (GraphExecution execution in task.Executions)
            {
                _registry.RestoreExecution(task, execution);
            }

            return task;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return [Error.Failure(FlowErrorCodes.RecordNodeMismatch, ex.Message)];
        }
    }

    private ExecutionInput BuildInput(TaskId task, ExecutableNode node, ExecutionRecord saved)
    {
        ExecutionInput input = new()
        {
            Task = task,
            Node = node.Path,
            Type = node.Type,
            ExecutionCount = saved.Attempt,
            Inputs =
            [
                .. saved.Inputs.Select(value => new InputValue(
                    new PortName(value.Port.Value),
                    new ContextValue(GraphRecordSources.Read(value.Source), value.Text))),
            ],
            Texts = saved.Texts.ToDictionary(pair => new PortName(pair.Key.Value), pair => pair.Value),
            OutputPorts = node.SubmittablePorts,
        };

        if (_targets is not null && _targets.For(node, input) is { IsError: false } declaration)
        {
            input = input with { Declaration = declaration.Value };
        }

        return input;
    }
}
