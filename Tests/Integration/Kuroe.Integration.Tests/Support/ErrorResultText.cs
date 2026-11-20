using ErrorOr;

namespace Kuroe.Integration.Tests.Support;

/// <summary>错误结果断言用的说明文本。</summary>
internal static class ErrorResultText
{
    internal static string ErrorsText<T>(this ErrorOr<T> result) =>
        result.IsError
            ? string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description))
            : string.Empty;
}