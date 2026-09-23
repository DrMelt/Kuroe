using Kuroe.Agent;

namespace Kuroe.Workflows;

/// <summary>任务流程模板：任务由哪些步骤组成。配置读写见 <see cref="WorkflowService"/>。</summary>
public sealed record Workflow(string Name, string? Description, IReadOnlyList<StepSpec> Steps)
{
    /// <summary>内置流程的名字：用户层未指定默认流程时使用，工作目录里还没有 flows.json 时也只有这一条。</summary>
    public const string BuiltinName = "默认";

    /// <summary>内置流程：规划交回条目，逐条实施，逐条检查，检查不通过退回返工。</summary>
    public static readonly Workflow Builtin = new(BuiltinName, "内置流程：规划、实施、检查",
    [
        new StepSpec
        {
            Name = "规划",
            Role = RunRole.Plan,
            Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
        },
        new StepSpec
        {
            Name = "实施",
            Role = RunRole.Implement,
            Scope = StepScope.PerItem,
            From = ["规划"],
        },
        new StepSpec
        {
            Name = "检查",
            Role = RunRole.Check,
            Scope = StepScope.PerItem,
            From = ["规划", "实施"],
            OnReject = RejectAction.Retry,
        },
    ]);

    /// <summary>步骤序号，名称不存在时为空。</summary>
    public int? IndexOf(string name)
    {
        for (int index = 0; index < Steps.Count; index++)
        {
            if (Steps[index].Name == name)
            {
                return index;
            }
        }

        return null;
    }

    public int Count => Steps.Count;
}
