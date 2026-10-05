using ErrorOr;

namespace Kuroe.Shared.Executions.Tools;

/// <summary>工具调用结果的交回文本渲染。错误逐条合并，前缀按错误类型区分。</summary>
public static class ToolResult
{
    /// <summary>渲染工具调用结果：错误时逐条拼类型前缀并合并成文本。</summary>
    public static string Render(ErrorOr<string> result) =>
        result.IsError
            ? string.Join("；", result.ErrorsOrEmptyList.Select(error => $"{Prefix(error.Type)}{error.Description}"))
            : result.Value;

    /// <summary>错误类型到交回文本前缀的映射：验证与查无归为拒绝，未预期为内部错误，其余为失败。</summary>
    private static string Prefix(ErrorType type) => type switch
    {
        ErrorType.Validation or ErrorType.NotFound => "被拒绝：",
        ErrorType.Unexpected => "内部错误：",
        _ => "失败：",
    };
}
