using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具路径：白名单匹配的原文，末段是函数名，模型调用名由路径派生；格式在入口校验。</summary>
public sealed class ToolPathTests
{
    [Fact(DisplayName = "名称是最后一段")]
    public void Name_is_the_last_segment()
    {
        Assert.Equal(new ToolName("GetTime"), new ToolPath("GetTime").Name);
        Assert.Equal(new ToolName("ReadFile"), new ToolPath("file/read/ReadFile").Name);
        Assert.Equal(new ToolName(string.Empty), new ToolPath(string.Empty).Name);
    }

    [Fact(DisplayName = "调用名压平分隔符")]
    public void Call_name_flattens_the_separators()
    {
        Assert.Equal(new ToolName("GetLocalTime"), new ToolPath("GetLocalTime").CallName);
        Assert.Equal(new ToolName("file_read_ReadFile"), new ToolPath("file/read/ReadFile").CallName);
        Assert.Equal(new ToolName("KuroeInfo_ListTasks"), new ToolPath("KuroeInfo/ListTasks").CallName);
    }

    [Theory]
    [InlineData("KuroeInfo", "KuroeInfo", Label = "条目自身")]
    [InlineData("KuroeInfo", "KuroeInfo/GetTask", Label = "条目子树")]
    [InlineData("KuroeInfo/GetTask", "KuroeInfo/GetTask", Label = "完整路径自身")]
    [InlineData("file", "file/read/ReadFile", Label = "三层路径")]
    public void Allows_accepts_the_entry_and_its_subtree(string entry, string path) =>
        Assert.True(new ToolPath(entry).Allows(path));

    [Theory]
    [InlineData("KuroeInfo", "KuroeInfoX", Label = "名称前缀相似")]
    [InlineData("KuroeInfo/GetTask", "KuroeInfo", Label = "父路径")]
    [InlineData("Kuroeinfo", "KuroeInfo", Label = "大小写不同")]
    public void Allows_rejects_paths_outside_the_entry(string entry, string path) =>
        Assert.False(new ToolPath(entry).Allows(path));

    [Fact(DisplayName = "空条目不允许任何路径")]
    public void Empty_entry_allows_nothing()
    {
        ToolPath entry = new(string.Empty);

        Assert.False(entry.Allows(string.Empty));
        Assert.False(entry.Allows("KuroeInfo"));
    }

    [Theory]
    [InlineData("/git", Label = "前导斜杠")]
    [InlineData("git/", Label = "末尾斜杠")]
    public void Invalid_reason_rejects_leading_or_trailing_slash(string value) =>
        Assert.Equal($"路径 {value} 不能以 / 开头或结尾。", ToolPath.InvalidReason(value));

    [Fact(DisplayName = "非法原因标明出错的分段")]
    public void Invalid_reason_names_the_offending_segment()
    {
        Assert.Equal("路径 gi t 的段 gi t 只能写字母、数字、下划线与连字符。", ToolPath.InvalidReason("gi t"));
        Assert.Equal("路径 a{b} 的段 a{b} 只能写字母、数字、下划线与连字符。", ToolPath.InvalidReason("a{b}"));
        Assert.Equal("路径 git/状态 的段 状态 只能写字母、数字、下划线与连字符。", ToolPath.InvalidReason("git/状态"));
        Assert.Equal("路径 git//x 的段不能为空。", ToolPath.InvalidReason("git//x"));
    }

    [Theory]
    [InlineData("", Label = "空字符串")]
    [InlineData("  ", Label = "空白")]
    [InlineData("KuroeInfo/GetTask", Label = "完整路径")]
    public void Invalid_reason_accepts_blank_or_well_formed_paths(string value) =>
        Assert.Null(ToolPath.InvalidReason(value));
}