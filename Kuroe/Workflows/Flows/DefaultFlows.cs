using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置模型选择与内置流程：用户层未指定默认流程时使用，工作目录里还没有 flows.json 时也只有这一条。
/// 三个模型选择都不写模型名，流程可加载但执行时无法确定模型，派发 run 报错。</summary>
internal static class DefaultFlows
{
    /// <summary>内置流程的名字。</summary>
    internal const string Name = "默认";

    /// <summary>规划者：交回条目拆分。</summary>
    internal static readonly ModelDefinition Planner = new()
    {
        Name = new ModelRef("规划者"),
    };

    /// <summary>执行者：实施条目。</summary>
    internal static readonly ModelDefinition Executor = new()
    {
        Name = new ModelRef("执行者"),
    };

    /// <summary>检查者：交回整体检查结论。</summary>
    internal static readonly ModelDefinition Reviewer = new()
    {
        Name = new ModelRef("检查者"),
    };

    /// <summary>内置流程：制定计划、按条目分配执行、整体检查，不通过退回返工。</summary>
    internal static readonly Workflow Builtin = new(Name, "内置流程：制定计划、分配执行、整体检查",
        [Planner, Executor, Reviewer],
        [
            new ExecutableNode
            {
                Name = new NodeName("制定计划"),
                Model = Planner.Name,
                Output = NodeOutput.Plan,
                Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
            },
            new ContainerNode
            {
                Name = new NodeName("交付"),
                Nodes =
                [
                    new ExecutableNode
                    {
                        Name = new NodeName("分配执行"),
                        Model = Executor.Name,
                        Tools = [new ToolName("GetLocalTime"), new ToolName("GetWeather")],
                        Mode = NodeMode.PerItem,
                        From = [new NodeName("制定计划")],
                    },
                    new ExecutableNode
                    {
                        Name = new NodeName("整体检查"),
                        Model = Reviewer.Name,
                        Output = NodeOutput.Review,
                        From = [new NodeName("制定计划"), new NodeName("分配执行")],
                        OnReject = RejectAction.Retry,
                        MaxAttempts = 2,
                    },
                ],
            },
        ]);
}