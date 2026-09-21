using ErrorOr;

namespace Kuroe.Cli;

/// <summary>启动期工作目录的确定：命令行优先，未指定时用进程当前目录。</summary>
internal static class WorkDirectory
{
    private const string LongOption = "--work-directory";
    private const string ShortOption = "-d";

    /// <summary>确定工作目录并返回绝对路径。</summary>
    public static ErrorOr<string> Resolve(string[] args)
    {
        ErrorOr<string?> option = Parse(args);
        if (option.IsError)
        {
            return option.ErrorsOrEmptyList;
        }

        return Path.GetFullPath(string.IsNullOrWhiteSpace(option.Value)
            ? Directory.GetCurrentDirectory()
            : option.Value);
    }

    /// <summary>从命令行取出工作目录，未指定时为 null。</summary>
    private static ErrorOr<string?> Parse(string[] args)
    {
        string? directory = null;
        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            if (!argument.Equals(LongOption, StringComparison.OrdinalIgnoreCase) &&
                !argument.Equals(ShortOption, StringComparison.OrdinalIgnoreCase))
            {
                return [Error.Validation("WorkDirectory.UnknownArgument", $"未知参数 {argument}，用法：{LongOption} <目录>")];
            }

            if (i + 1 == args.Length)
            {
                return [Error.Validation("WorkDirectory.MissingValue", $"{argument} 需要跟一个目录。")];
            }

            directory = args[++i];
        }

        return directory;
    }
}