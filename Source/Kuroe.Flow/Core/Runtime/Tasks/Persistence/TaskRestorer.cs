using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从检查点恢复任务对象：重建执行历史与节点状态，不启动推进循环。</summary>
public sealed class TaskRestorer(TaskRegistry registry) : ITaskRestorer
{
    private readonly TaskRegistry _registry = registry;

    /// <inheritdoc/>
    public ErrorOr<WorkTask> Restore(TaskCheckpoint checkpoint, CompiledFlow flow)
    {
        try
        {
            PortTexts startTexts = new(checkpoint.Start.StartTexts);
            StaticPortValues staticPorts = new(flow.Graph, startTexts);
            List<Execution> restored = [];
            foreach (ExecutionCheckpoint saved in checkpoint.Executions)
            {
                if (flow.Graph[saved.Node] is not ExecutableNode node)
                {
                    return [Error.Failure(
                        FlowErrorCodes.CheckpointGraphMismatch,
                        $"检查点执行节点 {saved.Node} 不在流程图中。")];
                }

                restored.Add(Execution.Restore(saved, BuildInput(checkpoint.Identity.Id, node, saved)));
            }

            WorkTask task = WorkTask.Restore(_registry, checkpoint, flow, startTexts, staticPorts, restored);
            foreach (Execution execution in task.Executions)
            {
                _registry.RestoreExecution(task, execution);
            }

            return task;
        }
        catch (ArgumentException ex)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointNodeMismatch, ex.Message)];
        }
    }

    private static ExecutionInput BuildInput(TaskId task, ExecutableNode node, ExecutionCheckpoint saved)
    {
        return new ExecutionInput
        {
            Task = task,
            Node = node.Path,
            Type = node.Type,
            ExecutionCount = saved.ExecutionCount,
            Inputs =
            [
                .. saved.Inputs.Select(value => new InputValue(
                    value.Port,
                    new ContextValue(CheckpointSources.Read(value.Origin), value.Text))),
            ],
            Texts = saved.Texts,
            OutputPorts = [.. node.Outputs.Where(port => !port.IsConstant)],
        };
    }
}
