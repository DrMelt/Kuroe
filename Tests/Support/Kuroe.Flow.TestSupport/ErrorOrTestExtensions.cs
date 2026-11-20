using ErrorOr;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试支持里的 ErrorOr 断言辅助：出错时把全部说明拼进异常。</summary>
public static class ErrorOrTestExtensions
{
    /// <summary>出错时抛出异常。</summary>
    public static T ThrowIfError<T>(this ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)))
            : result.Value;

    /// <summary>出错时抛出异常。</summary>
    public static void ThrowIfError(this ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));
        }
    }
}
