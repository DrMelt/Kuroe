using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.CommandTools;

/// <summary>命令工具的模板与参数文本呈现，宿主的 /tool list 与信息查询工具共用。</summary>
public static class CommandToolPresentation
{
    /// <summary>模板呈现：对象项在文字后加 ? 表示关联参数缺省时整项消失。</summary>
    public static string Template(IReadOnlyList<CommandToolTemplateItem> template) =>
        string.Join(" ", template.Select(item => item.OmitWhenMissing is null ? item.Text : $"{item.Text}?"));

    /// <summary>参数呈现：名字与是否列表、是否必填。</summary>
    public static string Parameters(IReadOnlyList<ToolParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("、", parameters.Select(parameter =>
            parameter.Name.Value
            + (parameter.List ? "[]" : string.Empty)
            + (parameter.Required ? "*" : string.Empty)));
    }
}