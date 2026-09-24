namespace Kuroe.Shared.Workflows.Flows;

/// <summary>任务流程模板：任务由哪些步骤组成。配置读写由 WorkflowService 负责。</summary>
public sealed record Workflow(string Name, string? Description, IReadOnlyList<StepSpec> Steps)
{
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

    /// <summary>步骤数。</summary>
    public int Count => Steps.Count;
}
