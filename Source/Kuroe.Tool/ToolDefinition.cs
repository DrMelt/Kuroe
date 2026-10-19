using ErrorOr;

namespace Kuroe.Tool;

/// <summary>工具函数的定义：路径、说明与参数。不含调用体与运行期状态。</summary>
/// <param name="Path">函数在工具层级里的完整路径，模型按它调用，白名单按它匹配。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明，宿主按它拼装 JSON Schema。</param>
public sealed record ToolDefinition(
    ToolPath Path,
    string Description,
    IReadOnlyList<ToolParameter> Parameters)
{
    /// <summary>把定义绑上调用体，得到模型可调用的函数。</summary>
    public ToolFunction Bind(Func<ToolArguments, ErrorOr<string>> invoke) => new(this, invoke);
}
