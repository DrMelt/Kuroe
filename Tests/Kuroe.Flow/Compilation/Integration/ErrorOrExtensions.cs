using ErrorOr;

namespace Kuroe.Flow.Compilation.Integration.Tests;

/// <summary>出错时把全部错误说明拼进异常信息。</summary>
public static class ErrorOrExtensions
{
    public static T ThrowIfError<T>(this ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)))
            : result.Value;
}
