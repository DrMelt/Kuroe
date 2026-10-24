using ErrorOr;

namespace Kuroe.Tool;

/// <summary>模型可调用的函数：定义与调用体。调用前先校验声明的路径参数，调用体把结果交回模型，失败时以 ErrorOr 表达。</summary>
public sealed class ToolFunction
{
    private readonly Func<ToolArguments, ErrorOr<string>> _invoke;
    private readonly ToolPathScope? _paths;

    internal ToolFunction(ToolDefinition definition, Func<ToolArguments, ErrorOr<string>> invoke, ToolPathScope? paths)
    {
        Definition = definition;
        _invoke = invoke;
        _paths = paths;
        Invoke = InvokeChecked;
    }

    /// <summary>函数的定义。</summary>
    public ToolDefinition Definition { get; }

    /// <summary>路径参数校验与调用体执行后的结果，失败时携带错误。</summary>
    public Func<ToolArguments, ErrorOr<string>> Invoke { get; }

    /// <summary>先校验路径参数，再执行调用体。</summary>
    private ErrorOr<string> InvokeChecked(ToolArguments arguments)
    {
        if (Check(arguments) is { } rejected)
        {
            return rejected;
        }

        return _invoke(arguments);
    }

    /// <summary>校验全部路径参数，返回第一条拒绝错误。</summary>
    private Error? Check(ToolArguments arguments)
    {
        foreach (ToolParameter parameter in Definition.Parameters)
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

                foreach (string pathValue in values)
                {
                    if (CheckValue(parameter, pathValue) is { } rejectedValue)
                    {
                        return rejectedValue;
                    }
                }

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

            if (CheckValue(parameter, text) is { } rejectedText)
            {
                return rejectedText;
            }
        }

        return null;
    }

    /// <summary>校验一个路径值：空值拒绝，其余按工具声明的路径范围解析。</summary>
    private Error? CheckValue(ToolParameter parameter, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ToolErrors.Argument($"路径参数 {parameter.Name.Value} 不能为空。");
        }

        ErrorOr<string> resolved = _paths!.Resolve(value, parameter.PathAccess);

        return resolved.IsError ? resolved.FirstError : null;
    }
}
