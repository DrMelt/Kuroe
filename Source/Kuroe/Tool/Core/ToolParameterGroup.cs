using ErrorOr;

namespace Kuroe.Tool;

/// <summary>互斥的参数组：同一次调用里至多给出组内一个参数，如项目与解决方案二选一。约束在调用工具函数时生效。</summary>
/// <param name="Parameters">组内参数名，按参数声明的顺序。</param>
public sealed record ToolParameterGroup(IReadOnlyList<ToolName> Parameters)
{
    /// <summary>校验这次调用至多给出组内一个参数，给出多个时拒绝。</summary>
    public ErrorOr<Success> Check(IReadOnlyList<ToolParameter> parameters, ToolArguments arguments)
    {
        IReadOnlyList<ToolName> given =
        [
            .. Parameters
                .Select(name => parameters.FirstOrDefault(parameter => parameter.Name == name))
                .OfType<ToolParameter>()
                .Where(parameter => parameter.IsGiven(arguments))
                .Select(parameter => parameter.Name),
        ];

        return given.Count > 1
            ? ToolErrors.Argument($"参数 {string.Join("、", given.Select(name => name.Value))} 只能给出一个。")
            : Result.Success;
    }

    /// <summary>组相对参数声明的合法性问题，合法时为空：组不能为空，组内参数名不能重复，且都必须在参数声明里。</summary>
    public string? InvalidReason(IReadOnlyList<ToolParameter> parameters)
    {
        HashSet<ToolName> names = [];

        if (Parameters.Count == 0)
        {
            return "不能为空。";
        }

        foreach (ToolName name in Parameters)
        {
            if (!names.Add(name))
            {
                return $"含重复参数 {name.Value}。";
            }

            if (parameters.All(parameter => parameter.Name != name))
            {
                return $"含未声明的参数 {name.Value}。";
            }
        }

        return null;
    }
}