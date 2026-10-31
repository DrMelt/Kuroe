using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Shared;
using Xunit;

namespace Kuroe.Tests.Configuration.Files;

/// <summary>启动参数里工作目录的解析。</summary>
public sealed class KuroePathsTests
{
    [Fact]
    public void Missing_or_blank_uses_the_process_directory()
    {
        string cwd = Directory.GetCurrentDirectory();

        Assert.Equal(cwd, KuroePaths.Resolve(null).Value.WorkingDirectory);
        Assert.Equal(cwd, KuroePaths.Resolve(string.Empty).Value.WorkingDirectory);
        Assert.Equal(cwd, KuroePaths.Resolve("   ").Value.WorkingDirectory);
    }

    [Fact]
    public void Relative_path_becomes_absolute()
    {
        Assert.Equal(Path.GetFullPath("workspace"), KuroePaths.Resolve("workspace").Value.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(".."), KuroePaths.Resolve("..").Value.WorkingDirectory);
    }

    [Fact]
    public void Invalid_path_is_rejected()
    {
        ErrorOr<KuroePaths> result = KuroePaths.Resolve("bad\u0000path");

        Assert.True(result.IsError);
        Assert.Equal(ErrorCodes.WorkDirectoryInvalid, result.FirstError.Code);
        Assert.Contains("不是合法路径", result.FirstError.Description);
    }
}