using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Json.Tests;

/// <summary>工具文件的注册与统一装载：注册序、同名覆盖、跨文件去重与错误收集。</summary>
public sealed class ToolFilesTests
{
    [Fact]
    public void Registry_keeps_registration_order()
    {
        ToolFileRegistry files = new();
        files.Register(Stub("a.json"));
        files.Register(Stub("b.json"));

        Assert.Equal(["a.json", "b.json"], files.Files.Select(file => file.FileName));
    }

    [Fact]
    public void Registry_replaces_the_same_file_name_in_place()
    {
        ToolFileRegistry files = new();
        FakeToolFile first = Stub("a.json");
        FakeToolFile second = Stub("A.JSON");

        files.Register(first);
        files.Register(second);

        Assert.Equal(["A.JSON"], files.Files.Select(file => file.FileName));
        Assert.Same(second, files.Files[0]);
    }

    [Fact]
    public void Load_returns_empty_when_nothing_is_registered()
    {
        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = ToolFiles.Load(new ToolFileRegistry(), "config", "work");

        Assert.False(loaded.IsError);
        Assert.Empty(loaded.Value);
    }

    [Fact]
    public void Load_merges_registered_files_in_registration_order()
    {
        ToolFileRegistry files = new();
        files.Register(Stub("a.json", Tool("a"), Tool("b")));
        files.Register(Stub("b.json", Tool("c")));

        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = ToolFiles.Load(files, "config", "work");

        Assert.False(loaded.IsError);
        Assert.Equal(
            [new ToolPath("a"), new ToolPath("b"), new ToolPath("c")],
            loaded.Value.Select(tool => tool.Definition.Path));
    }

    [Fact]
    public void Load_reports_duplicate_paths_across_files()
    {
        ToolFileRegistry files = new();
        files.Register(Stub("a.json", Tool("a")));
        files.Register(Stub("b.json", Tool("a"), Tool("b")));

        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = ToolFiles.Load(files, "config", "work");

        Assert.True(loaded.IsError);
        Assert.Contains(loaded.ErrorsOrEmptyList,
            error => error.Description.Contains("重复", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_collects_errors_from_every_file()
    {
        ToolFileRegistry files = new();
        files.Register(new FakeToolFile("a.json", ToolFileErrors.Format("a.json 不是合法的 JSON 形状。")));
        files.Register(Stub("b.json", Tool("b")));

        ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = ToolFiles.Load(files, "config", "work");

        Assert.True(loaded.IsError);
        Assert.Contains(loaded.ErrorsOrEmptyList,
            error => error.Description.Contains("不是合法的 JSON 形状", StringComparison.Ordinal));
    }

    /// <summary>测试用文件形状。</summary>
    private static FakeToolFile Stub(string name, params ConfiguredTool[] tools) => new(name, tools);

    /// <summary>测试用配置工具：定义与配置形态由路径给出，载体没有函数。</summary>
    private static ConfiguredTool Tool(string path) =>
        new(new ToolDefinition(new ToolPath(path), "说明", []), path, _ => new FakeTool());

    /// <summary>测试用文件形状：装载结果由构造时给定。</summary>
    private sealed class FakeToolFile(string fileName, ErrorOr<IReadOnlyList<ConfiguredTool>> loaded) : IToolFile
    {
        /// <summary>本文件层的文件名。</summary>
        public string FileName { get; } = fileName;

        /// <summary>按构造时给定的结果返回。</summary>
        public ErrorOr<IReadOnlyList<ConfiguredTool>> Load(string configDirectory, string workDirectory) => loaded;
    }

    /// <summary>测试用载体。</summary>
    private sealed class FakeTool : ITool
    {
        /// <summary>本载体的函数声明。</summary>
        public IReadOnlyList<ToolFunction> Functions { get; } = [];
    }
}