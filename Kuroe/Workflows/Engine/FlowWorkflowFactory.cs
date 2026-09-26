using Kuroe.Agent.Runs;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;
using FrameworkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>按流程模板为单个任务组装 Workflow 图并启动流式运行。一个任务一份图，
/// 执行器持有任务标识与流程依赖，不跨任务共享状态。</summary>
internal static class FlowWorkflowFactory
{
    /// <summary>启动该任务的流程运行。start 与所有叶子、相邻叶子、检查到被查实施各有边，
    /// 激活消息按目标执行器定向投递，自动流转与时序由执行器内部决定。</summary>
    public static StreamingRun Start(
        AgentTask task,
        TaskRegistry registry,
        RunDispatcher dispatcher,
        NodeModelResolver models,
        SettingsProvider settings)
    {
        FlowStartExecutor start = new(registry, task.Id);
        FlowLeafExecutor[] leaves = [.. task.Graph.Leaves.Select((_, index) =>
            new FlowLeafExecutor(registry, dispatcher, models, settings, task.Id, index))];

        WorkflowBuilder builder = new(start);
        foreach (FlowLeafExecutor leaf in leaves)
        {
            builder.AddEdge(start, leaf);
        }

        for (int index = 1; index < leaves.Length; index++)
        {
            builder.AddEdge(leaves[index - 1], leaves[index]);
        }

        for (int check = 0; check < leaves.Length; check++)
        {
            if (task.Graph[check].Output != NodeOutput.Review)
            {
                continue;
            }

            int? implement = task.Graph.FunnelReturn(check) ?? task.Graph.ImplementBefore(check);
            if (implement is { } at)
            {
                builder.AddEdge(leaves[check], leaves[at]);
            }
        }

        FrameworkWorkflow workflow = builder.WithName(task.Flow.Name).Build();

        return InProcessExecution.RunStreamingAsync(
            workflow, new FlowMessage { Intent = FlowIntent.Start }).AsTask().GetAwaiter().GetResult();
    }
}