using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Runner.File.Tests;

/// <summary>文件节点的读写边界：读取截断与写入长度上限。</summary>
public sealed class FileContentTests
{
    private const int ReadLimit = 8000;

    private const int WriteLimit = 40000;

    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact(DisplayName = "读取按上限截断")]
    public void Read_truncates_at_the_limit()
    {
        _files.AddFile("big.txt", new string('a', ReadLimit + 10));

        string content = FileContent.Read(_files, "big.txt", ReadLimit);

        Assert.Equal(ReadLimit + "…内容已截断".Length, content.Length);
        Assert.EndsWith("…内容已截断", content);
    }

    [Fact(DisplayName = "没有上限时读取返回整个文件")]
    public void Read_without_a_limit_returns_the_whole_file()
    {
        string expected = new('a', ReadLimit + 10);
        _files.AddFile("big.txt", expected);

        string content = FileContent.Read(_files, "big.txt", 0);

        Assert.Equal(expected, content);
    }

    [Fact(DisplayName = "写入拒绝超出上限的内容")]
    public void Write_rejects_content_over_the_limit()
    {
        string? failure = FileContent.Write(_files, "out.txt", new string('a', WriteLimit + 1), WriteLimit);

        Assert.Contains($"写入内容超过上限 {WriteLimit} 字符", failure);
        Assert.False(_files.FileExists("out.txt"));
    }

    [Fact(DisplayName = "写入在上限处保留内容")]
    public void Write_keeps_content_at_the_limit()
    {
        string content = new('a', WriteLimit);

        string? failure = FileContent.Write(_files, "out.txt", content, WriteLimit);

        Assert.Null(failure);
        Assert.Equal(content, _files.ReadText("out.txt"));
    }

    [Fact(DisplayName = "没有上限时写入保留长内容")]
    public void Write_without_a_limit_keeps_long_content()
    {
        string content = new('a', WriteLimit + 10);

        string? failure = FileContent.Write(_files, "out.txt", content, 0);

        Assert.Null(failure);
        Assert.Equal(content, _files.ReadText("out.txt"));
    }
}