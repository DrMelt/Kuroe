using System.Text.Json;
using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Workflows.Tasks;

/// <summary>规划步骤交回的一个条目，是按条目展开执行单元的依据。</summary>
public sealed record PlanItem(int Index, string Title, string Instruction, string Acceptance);

/// <summary>规划步骤的产出：由哪个 agent 交回的条目拆分。</summary>
public sealed record StepPlan(RunId Origin, IReadOnlyList<PlanItem> Items);

/// <summary>规划步骤交回的条目拆分：把模型给的 JSON 数组解析成条目，不合法时给出一条可回给模型的原因。</summary>
static class PlanItems
{
    /// <summary>一个方案最多交回的条目数。</summary>
    private const int Limit = 20;

    public static ErrorOr<IReadOnlyList<PlanItem>> Parse(string itemsJson)
    {
        List<PlanItemDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(itemsJson, PlanJson.Default.ListPlanItemDto);
        }
        catch (JsonException ex)
        {
            return [TaskErrors.Items($"不是合法 JSON：{ex.Message}")];
        }

        if (parsed is null || parsed.Count == 0)
        {
            return [TaskErrors.Items("条目为空")];
        }

        if (parsed.Count > Limit)
        {
            return [TaskErrors.Items($"条目 {parsed.Count} 个，超过上限 {Limit}")];
        }

        List<PlanItem> items = [];
        for (int index = 0; index < parsed.Count; index++)
        {
            PlanItemDto item = parsed[index];
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction))
            {
                return [TaskErrors.Items($"第 {index + 1} 个条目缺 Title 或 Instruction")];
            }

            items.Add(new PlanItem(index, item.Title.Trim(), item.Instruction.Trim(),
                item.Acceptance?.Trim() ?? string.Empty));
        }

        return items;
    }
}

/// <summary>模型交回的条目文本的形状，不含序号与去空白后的结果。</summary>
internal sealed class PlanItemDto
{
    public string? Title { get; set; }

    public string? Instruction { get; set; }

    public string? Acceptance { get; set; }
}
