using ErrorOr;

namespace Kuroe.Tool;

/// <summary>模型可调用的函数：定义与调用体。调用前先校验互斥参数组与声明的路径参数，调用体把结果交回模型，失败时以 ErrorOr 表达。</summary>
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

    /// <summary>参数校验与调用体执行后的结果，失败时携带错误。</summary>
    public Func<ToolArguments, ErrorOr<string>> Invoke { get; }

    /// <summary>先校验互斥参数组与路径参数，再执行调用体。</summary>
    private ErrorOr<string> InvokeChecked(ToolArguments arguments)
    {
        if ((CheckGroups(arguments) ?? Check(arguments)) is { } rejected)
        {
            return rejected;
        }

        return _invoke(arguments);
    }

    /// <summary>校验互斥参数组，返回第一条拒绝错误。</summary>
    private Error? CheckGroups(ToolArguments arguments)
    {
        foreach (ToolParameterGroup group in Definition.ParameterGroups)
        {
            ErrorOr<Success> checkedValue = group.Check(Definition.Parameters, arguments);
            if (checkedValue.IsError)
            {
                return checkedValue.FirstError;
            }
        }

        return null;
    }

    /// <summary>校验全部路径参数，返回第一条拒绝错误。没有路径范围时没有路径参数可校验。</summary>
    private Error? Check(ToolArguments arguments)
    {
        if (_paths is not { } paths)
        {
            return null;
        }

        ErrorOr<IReadOnlyDictionary<ToolName, object?>> resolved =
            ToolPathArguments.Resolve(Definition.Parameters, arguments, paths);

        return resolved.IsError ? resolved.FirstError : null;
    }
}