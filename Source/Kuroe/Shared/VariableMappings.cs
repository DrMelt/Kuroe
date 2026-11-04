namespace Kuroe.Shared;

/// <summary>变量映射的装载期规则：变量名合法，端口已声明且承载文本，变量集合与占位符一一对应。</summary>
public static class VariableMappings
{
    /// <summary>校验映射与占位符，返回全部错误。portIsText 对未声明端口返回 null。
    /// label 是报错里的变量类别，reference 是未引用变量的说明。</summary>
    public static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyList<string> placeholders,
        Func<string, bool?> portIsText,
        string label,
        string reference)
    {
        List<string> errors = [];
        foreach ((string variable, string port) in variables)
        {
            if (PathTemplate.InvalidNameReason(variable, label) is { } reason)
            {
                errors.Add(reason);
                continue;
            }

            bool? isText = portIsText(port);
            if (isText is null)
            {
                errors.Add($"{label}变量 {variable} 映射到未声明的输入端口 {port}。");
            }
            else if (isText == false)
            {
                errors.Add($"{label}变量 {variable} 映射到的输入端口 {port} 必须是 Text。");
            }
        }

        foreach (string placeholder in placeholders.Except(variables.Keys))
        {
            errors.Add($"{label}占位符 {{{placeholder}}} 没有对应的{label}变量映射。");
        }

        foreach (string variable in variables.Keys.Except(placeholders))
        {
            errors.Add($"{label}变量 {variable} 没有被 {reference} 引用。");
        }

        return errors;
    }
}