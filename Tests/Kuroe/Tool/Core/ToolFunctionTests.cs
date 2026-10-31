using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Tests;

/// <summary>工具函数的路径参数校验：调用体执行前完成解析、边界与准入判断。</summary>
public sealed class ToolFunctionTests
{
    [Fact]
    public void Path_outside_scope_is_rejected_before_invoking()
    {
        string root = Root();
        bool called = false;
        ToolFunction function = Definition(RequiredPath())
            .Bind(_ =>
            {
                called = true;

                return "已写入";
            }, new ToolPathScope(root, root, "工作目录"));

        ErrorOr<string> result = function.Invoke(Given(("path", Path.Combine("..", "escape.txt"))));

        Assert.True(result.IsError);
        Assert.Contains("不在工作目录内", result.FirstError.Description, StringComparison.Ordinal);
        Assert.False(called);
    }

    [Fact]
    public void Missing_required_path_is_rejected()
    {
        string root = Root();
        ToolFunction function = Definition(RequiredPath())
            .Bind(_ => "已写入", new ToolPathScope(root, root, "工作目录"));

        ErrorOr<string> result = function.Invoke(Given());

        Assert.True(result.IsError);
        Assert.Equal("缺少参数 path。", result.FirstError.Description);
    }

    [Fact]
    public void Empty_path_is_rejected()
    {
        string root = Root();
        ToolFunction function = Definition(RequiredPath())
            .Bind(_ => "已写入", new ToolPathScope(root, root, "工作目录"));

        ErrorOr<string> result = function.Invoke(Given(("path", "  ")));

        Assert.True(result.IsError);
        Assert.Equal("路径参数 path 不能为空。", result.FirstError.Description);
    }

    [Fact]
    public void Optional_path_absent_invokes_body()
    {
        string root = Root();
        bool called = false;
        ToolFunction function = new ToolDefinition(
                new ToolPath("List"),
                "列出",
                [new ToolParameter(new ToolName("path"), "路径", Path: true)])
            .Bind(_ =>
            {
                called = true;

                return "已列出";
            }, new ToolPathScope(root, root, "工作目录"));

        ErrorOr<string> result = function.Invoke(Given());

        Assert.False(result.IsError);
        Assert.Equal("已列出", result.Value);
        Assert.True(called);
    }

    private static readonly string[] stringArray = ["src/a", ".."];

    [Fact]
    public void Path_list_item_outside_scope_rejects_whole_call()
    {
        string root = Root();
        bool called = false;
        ToolFunction function = new ToolDefinition(
                new ToolPath("Read"),
                "读取",
                [new ToolParameter(new ToolName("paths"), "路径列表", Required: true, List: true, Path: true)])
            .Bind(_ =>
            {
                called = true;

                return "已读取";
            }, new ToolPathScope(root, root, "工作目录"));

        ErrorOr<string> result = function.Invoke(Given(("paths", stringArray)));

        Assert.True(result.IsError);
        Assert.Contains("不在工作目录内", result.FirstError.Description, StringComparison.Ordinal);
        Assert.False(called);
    }

    [Fact]
    public void Bind_rejects_path_parameter_without_scope()
    {
        ToolDefinition definition = Definition(RequiredPath());

        Assert.Throws<InvalidOperationException>(() => definition.Bind(_ => "已写入"));
    }

    [Fact]
    public void Bind_rejects_flag_and_path_on_same_parameter()
    {
        string root = Root();
        ToolDefinition definition = new(
            new ToolPath("Invalid"),
            "非法",
            [new ToolParameter(new ToolName("path"), "路径", Flag: true, Path: true)]);

        Assert.Throws<InvalidOperationException>(
            () => definition.Bind(_ => "结果", new ToolPathScope(root, root, "工作目录")));
    }

    [Fact]
    public void Bind_rejects_path_access_on_non_path_parameter()
    {
        string root = Root();
        ToolDefinition definition = new(
            new ToolPath("Invalid"),
            "非法",
            [new ToolParameter(new ToolName("text"), "文本", PathAccess: new ToolPathAccess([], []))]);

        Assert.Throws<InvalidOperationException>(
            () => definition.Bind(_ => "结果", new ToolPathScope(root, root, "工作目录")));
    }

    /// <summary>声明一个必填路径参数的定义。</summary>
    private static ToolDefinition Definition(ToolParameter parameter) =>
        new(new ToolPath("Write"), "写入", [parameter]);

    /// <summary>必填路径参数。</summary>
    private static ToolParameter RequiredPath() =>
        new(new ToolName("path"), "路径", Required: true, Path: true);

    /// <summary>一次调用的实参。</summary>
    private static ToolArguments Given(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value));

    /// <summary>临时测试根目录，路径不需要实际存在。</summary>
    private static string Root() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
}