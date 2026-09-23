using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class WorkflowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Workflow flow, int attemptLimit)
    {
        List<Error> errors = [];
        if (flow.Steps.Count == 0)
        {
            errors.Add(WorkflowErrors.Body(flow.Name, "至少要有个步骤。"));
        }

        List<int> plans = [];
        int fanOut = -1;
        for (int index = 0; index < flow.Steps.Count; index++)
        {
            if (flow.Steps[index].Role == RunRole.Plan)
            {
                plans.Add(index);
            }

            if (fanOut < 0 && flow.Steps[index].Scope == StepScope.PerItem)
            {
                fanOut = index;
            }
        }

        if (plans.Count > 1)
        {
            errors.Add(WorkflowErrors.Body(flow.Name, "一条流程只能有一个规划步骤。"));
        }

        HashSet<string> names = [];
        for (int index = 0; index < flow.Steps.Count; index++)
        {
            StepSpec step = flow.Steps[index];
            string label = string.IsNullOrWhiteSpace(step.Name) ? $"第 {index + 1} 步" : step.Name;
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "步骤名不能为空。"));
            }
            else if (!names.Add(step.Name))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "步骤名重复。"));
            }

            if (index == 0 && step.From.Count > 0)
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "第一个步骤没有上游，不能声明 From。"));
            }

            foreach (string from in step.From)
            {
                int? referenced = flow.IndexOf(from);
                if (referenced is null)
                {
                    errors.Add(WorkflowErrors.Step(flow.Name, label, $"From 指向不存在的步骤 {from}。"));
                }
                else if (referenced >= index)
                {
                    errors.Add(WorkflowErrors.Step(flow.Name, label, $"From 只能引用更早的步骤，{from} 不行。"));
                }
            }

            if (step.Scope == StepScope.PerItem && (plans.Count == 0 || index <= plans[0]))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "按条目展开的步骤必须排在规划步骤之后。"));
            }

            if (fanOut >= 0 && index > fanOut && step.Scope == StepScope.Single)
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "展开条目之后的步骤必须同样按条目展开。"));
            }

            if (step.Role == RunRole.Check && !step.From.Any(
                    from => flow.IndexOf(from) is { } at && flow.Steps[at].Role == RunRole.Implement))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "检查步骤必须用 From 引用被检查的实施产出。"));
            }

            if (step.Role == RunRole.Implement
                && flow.Steps.Skip(index + 1).All(candidate => candidate.Role != RunRole.Check))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "实施之后要有检查步骤。"));
            }

            if (step.Role != RunRole.Check && (step.OnReject is not null || step.MaxAttempts is not null))
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, "OnReject 与 MaxAttempts 只适用于检查步骤。"));
            }

            if (step.Role == RunRole.Check && step.MaxAttempts > attemptLimit)
            {
                errors.Add(WorkflowErrors.Step(flow.Name, label, $"MaxAttempts 超过 Agent:MaxAttempts={attemptLimit}。"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static string? Duplicated(IReadOnlyList<Workflow> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}
