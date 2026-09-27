using Kuroe.Agent.Runs;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;
using FrameworkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>按流程模板为单个任务组装 Workflow 图并启动流式运行。一个任务一份图，
/// 执行器持有任务标识与流程依赖，不跨任务共享状态。</summary>
internal static class FlowWorkflowFactory
{
    /// <summary>启动该任务的流程运行。start 与全部根执行节点、每条依赖边、检查返工边各有边，
    /// 激活消息按目标执行器定向投递，就绪判定与发布由执行器内部按边决定。</summary>
    public static StreamingRun Start(
        AgentTask task,
        TaskRegistry registry,
        RunDispatcher dispatcher,
        NodeModelResolver models)
    {
        FlowStartExecutor start = new(registry, task.Id);
        NodeExecutor[] executors = [.. task.Graph.ExecutableNodes.Select((_, index) =>
            new NodeExecutor(registry, dispatcher, models, task.Id, index))];

        WorkflowBuilder builder = new(start);
        foreach (NodeExecutor executor in executors)
        {
            builder.AddEdge(start, executor);
        }

        foreach (FlowEdge edge in task.Graph.Edges)
        {
            builder.AddEdge(executors[edge.From], executors[edge.To]);
        }

        // 检查返工的退回边：检查执行节点不通过时把实施来源重跑
        for (int check = 0; check < executors.Length; check++)
        {
            if (task.Graph[check].Output != NodeOutput.Review)
            {
                continue;
            }

            foreach (int source in task.Graph.CheckedSources(check))
            {
                builder.AddEdge(executors[check], executors[source]);
            }
        }

        FrameworkWorkflow workflow = builder.WithName(task.Flow.Name).Build();

        return InProcessExecution.RunStreamingAsync(
            workflow, new FlowMessage { Intent = FlowIntent.Start }).AsTask().GetAwaiter().GetResult();
    }
}