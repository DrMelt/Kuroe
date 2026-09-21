using ErrorOr;

namespace Kuroe.Cli;

/// <summary>启动参数由命令行框架解析，这里把工作目录取值规范化为绝对路径。</summary>
internal static class WorkDirectory
{
    /// <summary>确定工作目录并返回绝对路径，未指定或为空白时用进程当前目录。</summary>
    public static ErrorOr<string> Resolve(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Directory.GetCurrentDirectory();
        }

        try
        {
            return Path.GetFullPath(directory);
        }
        catch (ArgumentException)
        {
            return Error.Validation("WorkDirectory.Invalid", $"工作目录不是合法路径：{directory}");
        }
    }
}