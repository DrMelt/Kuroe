using ErrorOr;

namespace Kuroe.Tool;

/// <summary>一次调用里的路径参数：按参数声明取出实参，校验后解析为根目录内的绝对路径，保持实参的标量与列表形态。
/// 路径参数的校验与绝对化都在这里，工具函数用它校验，命令工具用它的结果替换实参后展开模板。</summary>
public static class ToolPathArguments
{
    /// <summary>解析全部路径参数，返回按参数名给出的替换值，未给出的参数不出现在结果里。
    /// 任一参数不通过时交回第一条拒绝错误。</summary>
    public static ErrorOr<IReadOnlyDictionary<ToolName, object?>> Resolve(
        IReadOnlyList<ToolParameter> parameters,
        ToolArguments arguments,
        ToolPathScope paths)
    {
        Dictionary<ToolName, object?> resolved = [];

        foreach (ToolParameter parameter in parameters)
        {
            if (!parameter.Path)
            {
                continue;
            }

            if (parameter.List)
            {
                IReadOnlyList<string> values = arguments.List(parameter.Name) ?? [];
                if (values.Count == 0)
                {
                    if (parameter.Required)
                    {
                        return ToolErrors.Argument($"缺少参数 {parameter.Name.Value}。");
                    }

                    continue;
                }

                List<string> absolute = [];
                foreach (string value in values)
                {
                    ErrorOr<string> path = ResolveValue(paths, parameter, value);
                    if (path.IsError)
                    {
                        return path.ErrorsOrEmptyList;
                    }

                    absolute.Add(path.Value);
                }

                resolved[parameter.Name] = absolute;
                continue;
            }

            string? text = arguments.Text(parameter.Name);
            if (text is null)
            {
                if (parameter.Required)
                {
                    return ToolErrors.Argument($"缺少参数 {parameter.Name.Value}。");
                }

                continue;
            }

            ErrorOr<string> resolvedText = ResolveValue(paths, parameter, text);
            if (resolvedText.IsError)
            {
                return resolvedText.ErrorsOrEmptyList;
            }

            resolved[parameter.Name] = resolvedText.Value;
        }

        return resolved;
    }

    /// <summary>校验并解析一个路径值：空白与 : 开头拒绝，其余按路径范围解析为绝对路径。</summary>
    private static ErrorOr<string> ResolveValue(ToolPathScope paths, ToolParameter parameter, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ToolErrors.Argument($"路径参数 {parameter.Name.Value} 不能为空。");
        }

        if (value.StartsWith(':'))
        {
            return ToolErrors.Argument($"路径参数 {parameter.Name.Value} 不能以 : 开头。");
        }

        return paths.Resolve(value, parameter.PathAccess);
    }
}