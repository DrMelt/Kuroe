using ErrorOr;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>把配置的拆分源与模型交回的补充条目合成最终拆分。纯函数，收口点唯一使用。
/// 静态条目在前、补充条目在后，统一验收注入空缺，序号整体重排。</summary>
static class SplitMerge
{
    public static ErrorOr<IReadOnlyList<PlanItem>> Apply(SplitConfig split, IReadOnlyList<PlanItem> proposed)
    {
        if (split.ExtrasMax is { } limit && proposed.Count > limit)
        {
            return [TaskErrors.Items($"本节点最多补充 {limit} 条，收到 {proposed.Count} 条")];
        }

        List<PlanItem> merged = [];
        int sequence = 0;
        if (split.Items is { } items)
        {
            foreach (SplitItem item in items)
            {
                merged.Add(new PlanItem(sequence++, item.Title, item.Instruction, item.Acceptance, item.Branch));
            }
        }

        foreach (PlanItem item in proposed)
        {
            merged.Add(new PlanItem(sequence++, item.Title, item.Instruction,
                split.Acceptance ?? item.Acceptance, item.Branch));
        }

        return merged;
    }
}