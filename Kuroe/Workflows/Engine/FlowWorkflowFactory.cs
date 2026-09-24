using Kuroe.Agent.Runs;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;
using FrameworkWorkflow = Microsoft.Agents.AI.Workflows.Workflow;

namespace Kuroe.Workflows.Engine;

/// <summary>按流程模板为单个任务组装 Workflow 图并启动流式运行。一个任务一份图，
/// 执行器持有任务标识与流程依赖，不跨任务共享状态。</summary>
internal static class FlowWorkflowFactory
{
    /// <summary>启动该任务的流程运行。start 与所有步骤、相邻步骤、检查到前置实施各有边，
    /// 激活消息按目标执行器定向投递，自动流转与时序由执行器内部决定。</summary>
    public static StreamingRun Start(
        AgentTask task,
        TaskRegistry registry,
        RunDispatcher dispatcher,
        StepModelResolver models,
        SettingsProvider settings)
    {
        FlowStartExecutor start = new(registry, task.Id, task.Flow);
        FlowStepExecutor[] steps = [.. task.Flow.Steps.Select((spec, index) =>
            new FlowStepExecutor(registry, dispatcher, models, settings, task.Id, index, task.Flow))];

        WorkflowBuilder builder = new(start);
        foreach (FlowStepExecutor step in steps)
        {
            builder.AddEdge(start, step);
        }

        for (int index = 1; index < steps.Length; index++)
        {
            builder.AddEdge(steps[index - 1], steps[index]);
        }

        for (int check = 0; check < steps.Length; check++)
        {
            if (task.Flow.Steps[check].Role != RunRole.Check)
            {
                continue;
            }

            for (int implement = check - 1; implement >= 0; implement--)
            {
                if (task.Flow.Steps[implement].Role == RunRole.Implement)
                {
                    builder.AddEdge(steps[check], steps[implement]);
                    break;
                }
            }
        }

        FrameworkWorkflow workflow = builder.WithName(task.Flow.Name).Build();

        return InProcessExecution.RunStreamingAsync(
            workflow, new FlowMessage { Intent = FlowIntent.Start }).GetAwaiter().GetResult();
    }
}