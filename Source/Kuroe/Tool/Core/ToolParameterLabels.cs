namespace Kuroe.Tool;

/// <summary>工具参数的展示名。</summary>
public static class ToolParameterLabels
{
    /// <summary>参数表的展示：列表参数加 []，必填参数加 *，参数之间用、分隔。</summary>
    public static string Labels(this IReadOnlyList<ToolParameter> parameters) =>
        parameters.Count == 0
            ? string.Empty
            : string.Join("、", parameters.Select(parameter =>
                parameter.Name.Value
                + (parameter.List ? "[]" : string.Empty)
                + (parameter.Required ? "*" : string.Empty)));
}