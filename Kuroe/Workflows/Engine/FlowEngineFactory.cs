using Kuroe.Executions.Runs;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;
using FrameworkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>按流程模板为单个任务组装 Workflow 图并启动流式运行。一个任务一份图，
/// 执行器持有任务标识与流程依赖，不跨任务共享状态。</summary>
internal static class FlowEngineFactory
{
    /// <summary>启动该任务的流程运行。start 与全部根执行节点、每条依赖边各有边，
    /// 激活消息按目标执行器定向投递，就绪判定与发布由执行器内部按边决定。</summary>
    public static StreamingRun Start(
        WorkTask task,
        TaskRegistry registry,
        RunDispatcher dispatcher,
        NodeModelResolver models)
    {
        FlowStartExecutor start = new(registry, task.Id);
        Dictionary<int, NodeExecutor> byIndex = task.Runtime.Executables
            .ToDictionary(node => node.Index, node => new NodeExecutor(registry, dispatcher, models, task.Id, node));

        WorkflowBuilder builder = new(start);
        foreach (NodeExecutor executor in byIndex.Values)
        {
            builder.AddEdge(start, executor);
        }

        // 执行依赖与容器来源边统一路由：执行节点来源展开为自身，容器来源展开到容器内全部成员接到目标，
        // 让目标在框架图中可达并承接容器齐备的广播，就绪与否由推进器的放行判定保证
        foreach (FlowEdge edge in task.Graph.Edges)
        {
            foreach (int member in task.Graph.ExecutablesIn(edge.From))
            {
                builder.AddEdge(byIndex[member], byIndex[edge.To]);
            }
        }

        FrameworkWorkflow workflow = builder.WithName(task.Flow.Name).Build();

        return InProcessExecution.RunStreamingAsync(
            workflow, new FlowMessage { Intent = FlowIntent.Start }).AsTask().GetAwaiter().GetResult();
    }
}
