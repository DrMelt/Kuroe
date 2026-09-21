using ErrorOr;

namespace Kuroe;

/// <summary>错误到终端的统一输出，每条一行，走 stderr。</summary>
internal static class Errors
{
    public static void Report(IEnumerable<Error> errors)
    {
        foreach (Error error in errors)
        {
            Console.Error.WriteLine($"错误：{error.Code}：{error.Description}");
        }
    }
}
