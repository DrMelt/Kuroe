using ErrorOr;

namespace Kuroe.Shared.Executions.Tools;

/// <summary>工具函数的定义：名字、说明、参数与所属分组。不含调用体与运行期状态。</summary>
/// <param name="Name">函数名，模型按它调用。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Parameters">参数声明，宿主按它拼装 JSON Schema。</param>
/// <param name="Group">所属分组，构成完整路径的分组段；未给时完整路径即函数名。</param>
public sealed record ToolDefinition(
    ToolName Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters,
    ToolPath? Group = null)
{
    /// <summary>函数在工具层级里的完整路径，白名单按它匹配。</summary>
    public ToolPath Path => ToolPath.Of(Name, Group);

    /// <summary>把定义绑上调用体，得到模型可调用的函数。</summary>
    public ToolFunction Bind(Func<ToolArguments, ErrorOr<string>> invoke) => new(this, invoke);
}
