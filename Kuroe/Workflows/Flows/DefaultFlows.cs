using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置 agent 定义与内置流程：用户层未指定默认流程时使用，工作目录里还没有 flows.json 时也只有这一条。</summary>
internal static class DefaultFlows
{
    /// <summary>内置流程的名字。</summary>
    internal const string Name = "默认";

    /// <summary>规划者：交回条目拆分，不背能力工具。</summary>
    internal static readonly AgentDefinition Planner = new()
    {
        Name = "规划者",
        Description = "拆分目标，交回可独立实施的条目。",
    };

    /// <summary>执行者：实施条目，带时间与天气两个示例能力工具。</summary>
    internal static readonly AgentDefinition Executor = new()
    {
        Name = "执行者",
        Description = "实施条目，产出正文。",
        Tools = ["GetLocalTime", "GetWeather"],
    };

    /// <summary>检查者：交回整体检查结论，不背能力工具。</summary>
    internal static readonly AgentDefinition Reviewer = new()
    {
        Name = "检查者",
        Description = "检查实施产出，交回通过与否的结论。",
    };

    /// <summary>内置流程：制定计划、按条目分配执行、整体检查，不通过退回返工。</summary>
    internal static readonly Workflow Builtin = new(Name, "内置流程：制定计划、分配执行、整体检查",
        [Planner, Executor, Reviewer],
        [
            new AgentNode
            {
                Name = "制定计划",
                Agent = Planner.Name,
                Output = NodeOutput.Plan,
                Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
            },
            new FlowNode
            {
                Name = "交付",
                Nodes =
                [
                    new AgentNode
                    {
                        Name = "分配执行",
                        Agent = Executor.Name,
                        Mode = NodeMode.PerItem,
                        From = ["制定计划"],
                    },
                    new AgentNode
                    {
                        Name = "整体检查",
                        Agent = Reviewer.Name,
                        Output = NodeOutput.Review,
                        From = ["制定计划", "分配执行"],
                        OnReject = RejectAction.Retry,
                        MaxAttempts = 2,
                    },
                ],
            },
        ]);
}