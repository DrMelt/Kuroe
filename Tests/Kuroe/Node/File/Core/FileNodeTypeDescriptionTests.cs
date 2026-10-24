using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Xunit;

namespace Kuroe.Node.File.Tests;

/// <summary>文件节点的描述契约：字段表、路径解析与边界校验。</summary>
public sealed class FileNodeTypeDescriptionTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-file-description-tests"));

    [Fact]
    public void Read_description_declares_the_kind_shape_and_fields()
    {
        FileReadNodeTypeDescription description = new(Root);

        Assert.Equal(FileReadNodeTypes.Name, description.Kind);
        Assert.Equal(FileReadNodeTypes.Shape, description.Shape);
        Assert.Equal(
            [
                new ParameterField("Path", ParameterValueKind.Text),
                new ParameterField("MaxChars", ParameterValueKind.Integer),
            ],
            description.Fields);
    }

    [Fact]
    public void Write_description_declares_the_kind_shape_and_fields()
    {
        FileWriteNodeTypeDescription description = new(Root);

        Assert.Equal(FileWriteNodeTypes.Name, description.Kind);
        Assert.Equal(FileWriteNodeTypes.Shape, description.Shape);
        Assert.Equal(
            [
                new ParameterField("Path", ParameterValueKind.Text),
                new ParameterField("MaxChars", ParameterValueKind.Integer),
            ],
            description.Fields);
    }

    [Fact]
    public void Read_resolves_the_path_inside_the_working_directory()
    {
        List<string> failures = [];
        INodeType type = new FileReadNodeTypeDescription(Root).Read(new Reader("a/b.txt"), failures.Add);

        FileParameters parameters = Assert.IsType<FileReadNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal("a/b.txt", parameters.Path);
        Assert.Equal(Path.Combine(Root, "a", "b.txt"), parameters.FullPath);
        Assert.Equal(FileReadNodeTypes.DefaultMaxChars, parameters.MaxChars);
    }

    [Fact]
    public void Write_resolves_the_path_inside_the_working_directory()
    {
        List<string> failures = [];
        INodeType type = new FileWriteNodeTypeDescription(Root).Read(new Reader("a/b.txt"), failures.Add);

        FileParameters parameters = Assert.IsType<FileWriteNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal("a/b.txt", parameters.Path);
        Assert.Equal(Path.Combine(Root, "a", "b.txt"), parameters.FullPath);
        Assert.Equal(FileWriteNodeTypes.DefaultMaxChars, parameters.MaxChars);
    }

    [Fact]
    public void Missing_path_is_reported()
    {
        List<string> failures = [];
        _ = new FileReadNodeTypeDescription(Root).Read(new Reader(), failures.Add);

        Assert.Equal(["文件读取节点必须声明 Path。"], failures);
    }

    [Fact]
    public void Path_outside_the_working_directory_is_reported()
    {
        List<string> failures = [];
        _ = new FileWriteNodeTypeDescription(Root).Read(new Reader("../escape.txt"), failures.Add);

        Assert.Equal(["Path ../escape.txt 不是工作目录内的合法路径。"], failures);
    }

    [Fact]
    public void MaxChars_is_read_for_read_and_write()
    {
        List<string> failures = [];

        FileParameters read = Assert.IsType<FileReadNodeType>(
            new FileReadNodeTypeDescription(Root).Read(new Reader("a.txt", 1200), failures.Add)).Parameters!;
        FileParameters write = Assert.IsType<FileWriteNodeType>(
            new FileWriteNodeTypeDescription(Root).Read(new Reader("a.txt", 1200), failures.Add)).Parameters!;

        Assert.Empty(failures);
        Assert.Equal(1200, read.MaxChars);
        Assert.Equal(1200, write.MaxChars);
    }

    [Fact]
    public void Negative_MaxChars_is_reported()
    {
        List<string> failures = [];
        _ = new FileReadNodeTypeDescription(Root).Read(new Reader("a.txt", -1), failures.Add);

        Assert.Equal(["MaxChars 不能是负数。"], failures);
    }

    [Fact]
    public void Registration_adds_both_file_kinds()
    {
        RecordingRegistry registry = new();

        FileNodeTypeDescriptions.RegisterInto(registry, Root);

        Assert.Equal([FileReadNodeTypes.Name, FileWriteNodeTypes.Name], registry.Kinds);
    }

    /// <summary>测试用的参数段取值：只给出 Path 与 MaxChars 字段。</summary>
    private sealed class Reader(string? path = null, int? maxChars = null) : IParameterReader
    {
        public string? Text(string name) => name == "Path" ? path : null;

        public IReadOnlyList<string> Texts(string name) => [];

        public int? Integer(string name) => name == "MaxChars" ? maxChars : null;
    }

    /// <summary>测试用的描述注册表：记录注册序并按名查找。</summary>
    private sealed class RecordingRegistry : INodeTypeDescriptionRegistry
    {
        private readonly List<INodeTypeDescription> _descriptions = [];

        public void Register(INodeTypeDescription description) => _descriptions.Add(description);

        public INodeTypeDescription? Find(NodeKind kind) =>
            _descriptions.FirstOrDefault(description => description.Kind == kind);

        public INodeTypeDescription? Find(string name) =>
            _descriptions.FirstOrDefault(description => description.Kind.Value.Equals(name, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<NodeKind> Kinds => [.. _descriptions.Select(description => description.Kind)];
    }
}