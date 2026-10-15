using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;

namespace Kuroe.Flow.Execution.Submissions;

/// <summary>声明 WorkItems 端口的执行节点交回的条目拆分：把模型给的 JSON 数组解析成条目，不合法时给出一条可回给模型的原因。</summary>
static class WorkItemParser
{
    /// <summary>一个方案最多交回的条目数。</summary>
    private const int Limit = 20;

    public static ErrorOr<IReadOnlyList<WorkItem>> Parse(string itemsJson)
    {
        List<WorkItemDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(itemsJson, WorkItemJson.Default.ListWorkItemDto);
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

        List<WorkItem> items = [];
        for (int index = 0; index < parsed.Count; index++)
        {
            WorkItemDto item = parsed[index];
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction))
            {
                return [TaskErrors.Items($"第 {index + 1} 个条目缺 Title 或 Instruction")];
            }

            BranchName? branch = string.IsNullOrWhiteSpace(item.Branch) ? null : new BranchName(item.Branch.Trim());
            items.Add(new WorkItem(index, item.Title.Trim(), item.Instruction.Trim(),
                item.Acceptance?.Trim() ?? string.Empty, branch));
        }

        return items;
    }
}
