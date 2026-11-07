using ErrorOr;

namespace Kuroe.Tool;

/// <summary>工具调用结果的交回文本渲染。成功取返回文本，失败把各错误说明合并成文本。</summary>
public static class ToolResult
{
    /// <summary>渲染工具调用结果。</summary>
    public static string Render(ErrorOr<string> result) =>
        result.IsError
            ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description))
            : result.Value;
}
