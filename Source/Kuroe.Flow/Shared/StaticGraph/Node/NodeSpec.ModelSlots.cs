namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>模型槽位声明规则：名字非空且不重复。</summary>
public sealed partial record NodeSpec
{
    /// <summary>检查槽位声明。</summary>
    public void CheckModelSlots(Action<string> fail)
    {
        if (ModelSlots is not { Count: > 0 } slots)
        {
            return;
        }

        foreach (ModelRef slot in slots.Where(slot => string.IsNullOrWhiteSpace(slot.Value)))
        {
            fail("模型槽位名不能为空。");
        }

        foreach (ModelRef duplicated in slots.GroupBy(slot => slot).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            fail($"模型槽位 {duplicated} 重复。");
        }
    }
}