using ErrorOr;
using Kuroe.Host.Composition.Configuration.Files;
using Kuroe.Host.Platform;
using Xunit;

namespace Kuroe.Host.Composition.Tests.Configuration.Files;

/// <summary>启动参数里工作目录的解析。</summary>
public sealed class KuroePathsTests
{
    [Fact(DisplayName = "缺失或空白时使用进程目录")]
    public void Missing_or_blank_uses_the_process_directory()
    {
        string cwd = Directory.GetCurrentDirectory();

        Assert.Equal(cwd, KuroePaths.Resolve(null).Value.WorkingDirectory);
        Assert.Equal(cwd, KuroePaths.Resolve(string.Empty).Value.WorkingDirectory);
        Assert.Equal(cwd, KuroePaths.Resolve("   ").Value.WorkingDirectory);
    }

    [Fact(DisplayName = "相对路径转为绝对路径")]
    public void Relative_path_becomes_absolute()
    {
        Assert.Equal(Path.GetFullPath("workspace"), KuroePaths.Resolve("workspace").Value.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(".."), KuroePaths.Resolve("..").Value.WorkingDirectory);
    }

    [Fact(DisplayName = "助手记录位于助手目录下")]
    public void Assistant_records_live_under_the_assistant_directory()
    {
        KuroePaths paths = KuroePaths.At(Path.Combine(Path.GetTempPath(), "kuroe-paths"));

        Assert.Equal(Path.Combine(paths.WorkingDirectory, ".kuroe_assistant"), paths.AssistantDirectory);
        Assert.Equal(Path.Combine(paths.AssistantDirectory, "records"), paths.AssistantRecordsDirectory);
        Assert.NotEqual(paths.RecordsDirectory, paths.AssistantRecordsDirectory);
    }

    [Fact(DisplayName = "非法路径被拒绝")]
    public void Invalid_path_is_rejected()
    {
        ErrorOr<KuroePaths> result = KuroePaths.Resolve("bad\u0000path");

        Assert.True(result.IsError);
        Assert.Equal(ErrorCodes.WorkDirectoryInvalid, result.FirstError.Code);
        Assert.Contains("不是合法路径", result.FirstError.Description);
    }
}