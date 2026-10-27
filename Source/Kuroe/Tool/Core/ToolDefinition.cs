using ErrorOr;

namespace Kuroe.Tool;

/// <summary>工具函数的定义：路径、说明、参数与路径变量。不含调用体与运行期状态。</summary>
/// <param name="Path">函数在工具层级里的完整路径，模型按它调用，白名单按它匹配。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明，宿主按它拼装 JSON Schema。</param>
/// <param name="Variables">路径变量名，值由使用处的端口映射提供，对模型不可见。</param>
public sealed record ToolDefinition(
    ToolPath Path,
    string Description,
    IReadOnlyList<ToolParameter> Parameters,
    IReadOnlyList<string>? Variables = null)
{
    /// <summary>路径变量名集合，未声明时为空。</summary>
    public IReadOnlyList<string> PathVariables => Variables ?? [];
    /// <summary>把定义绑上调用体，得到模型可调用的函数。声明了路径参数时必须给出路径范围。</summary>
    public ToolFunction Bind(Func<ToolArguments, ErrorOr<string>> invoke, ToolPathScope? paths = null)
    {
        if (Parameters.Any(parameter => parameter.Path && parameter.Flag))
        {
            throw new InvalidOperationException($"工具 {Path.Value} 的参数不能同时是布尔与路径。");
        }

        if (Parameters.Any(parameter => parameter.PathAccess is not null && !parameter.Path))
        {
            throw new InvalidOperationException($"工具 {Path.Value} 的非路径参数不能配置路径准入规则。");
        }

        if (paths is null && Parameters.Any(parameter => parameter.Path))
        {
            throw new InvalidOperationException($"工具 {Path.Value} 的路径参数没有路径范围。");
        }

        return new(this, invoke, paths);
    }
}
