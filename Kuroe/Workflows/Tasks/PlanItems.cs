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

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public static ErrorOr<IReadOnlyList<PlanItem>> Parse(string itemsJson)
    {
        List<ItemDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<ItemDto>>(itemsJson, ReadOptions);
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
            ItemDto item = parsed[index];
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction))
            {
                return [TaskErrors.Items($"第 {index + 1} 个条目缺 Title 或 Instruction")];
            }

            items.Add(new PlanItem(index, item.Title.Trim(), item.Instruction.Trim(),
                item.Acceptance?.Trim() ?? string.Empty));
        }

        return items;
    }

    private sealed class ItemDto
    {
        public string? Title { get; init; }

        public string? Instruction { get; init; }

        public string? Acceptance { get; init; }
    }
}
