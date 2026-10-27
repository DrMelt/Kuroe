namespace Kuroe.Flow.Shared.Execution;

/// <summary>上下文取值的文本拼合：非空段按来源顺序以空行连接，没有内容时为空。</summary>
public static class ContextValues
{
    /// <summary>把取值拼成一段文本。</summary>
    public static string? Join(IEnumerable<ContextValue> values)
    {
        string[] parts = [.. values.Select(value => value.Text).Where(text => text.Length > 0)];

        return parts.Length == 0 ? null : string.Join("\n\n", parts);
    }
}