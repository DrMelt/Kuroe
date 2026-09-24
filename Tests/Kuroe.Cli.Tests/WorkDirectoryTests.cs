using ErrorOr;
using Kuroe.Cli;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>启动参数里工作目录的解析。</summary>
public sealed class WorkDirectoryTests
{
    [Fact]
    public void Missing_or_blank_uses_the_process_directory()
    {
        string cwd = Directory.GetCurrentDirectory();

        Assert.Equal(cwd, WorkDirectory.Resolve(null).Value);
        Assert.Equal(cwd, WorkDirectory.Resolve(string.Empty).Value);
        Assert.Equal(cwd, WorkDirectory.Resolve("   ").Value);
    }

    [Fact]
    public void Relative_path_becomes_absolute()
    {
        Assert.Equal(Path.GetFullPath("workspace"), WorkDirectory.Resolve("workspace").Value);
        Assert.Equal(Path.GetFullPath(".."), WorkDirectory.Resolve("..").Value);
    }

    [Fact]
    public void Invalid_path_is_rejected()
    {
        ErrorOr<string> result = WorkDirectory.Resolve("bad\u0000path");

        Assert.True(result.IsError);
        Assert.Equal("WorkDirectory.Invalid", result.FirstError.Code);
        Assert.Contains("不是合法路径", result.FirstError.Description);
    }
}