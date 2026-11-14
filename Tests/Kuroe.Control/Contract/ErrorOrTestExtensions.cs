using ErrorOr;

namespace Kuroe.Control.Contract.Tests;

/// <summary>测试里的错误解包：出错时把全部错误说明拼进异常信息。</summary>
internal static class ErrorOrTestExtensions
{
    public static T ThrowIfError<T>(this ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)))
            : result.Value;
}