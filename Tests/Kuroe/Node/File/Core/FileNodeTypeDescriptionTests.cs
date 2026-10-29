using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Node.File.Tests;

/// <summary>文件节点的描述契约：字段表与参数读取。</summary>
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
                new ParameterField("Variables", ParameterValueKind.Map),
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
                new ParameterField("Variables", ParameterValueKind.Map),
                new ParameterField("MaxChars", ParameterValueKind.Integer),
            ],
            description.Fields);
    }

    [Fact]
    public void Read_keeps_the_path_and_the_workspace_root()
    {
        List<string> failures = [];
        INodeType type = new FileReadNodeTypeDescription(Root).Read(new Reader("a/b.txt"), failures.Add);

        FileParameters parameters = Assert.IsType<FileReadNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal("a/b.txt", parameters.Path);
        Assert.Equal(Root, parameters.RootDirectory);
        Assert.Equal(FileReadNodeTypes.DefaultMaxChars, parameters.MaxChars);
    }

    [Fact]
    public void Write_keeps_the_path_and_the_workspace_root()
    {
        List<string> failures = [];
        INodeType type = new FileWriteNodeTypeDescription(Root).Read(new Reader("a/b.txt"), failures.Add);

        FileParameters parameters = Assert.IsType<FileWriteNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal("a/b.txt", parameters.Path);
        Assert.Equal(Root, parameters.RootDirectory);
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
    public void Variables_are_read_for_read_and_write()
    {
        Dictionary<string, string> variables = new() { ["root"] = "任务路径" };
        List<string> failures = [];

        FileParameters read = Assert.IsType<FileReadNodeType>(
            new FileReadNodeTypeDescription(Root).Read(new Reader("{root}/a.txt", variables: variables), failures.Add)).Parameters!;
        FileParameters write = Assert.IsType<FileWriteNodeType>(
            new FileWriteNodeTypeDescription(Root).Read(new Reader("{root}/a.txt", variables: variables), failures.Add)).Parameters!;

        Assert.Empty(failures);
        Assert.Equal(variables, read.Variables);
        Assert.Equal(variables, write.Variables);
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
    private sealed class Reader(
        string? path = null,
        int? maxChars = null,
        IReadOnlyDictionary<string, string>? variables = null) : IParameterReader
    {
        public string? Text(string name) => name == "Path" ? path : null;

        public IReadOnlyList<string> Texts(string name) => [];

        public int? Integer(string name) => name == "MaxChars" ? maxChars : null;

        public IReadOnlyDictionary<string, string> Mappings(string name) =>
            name == "Variables" ? variables ?? new Dictionary<string, string>() : new Dictionary<string, string>();

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> MappingMaps(string name) =>
            new Dictionary<string, IReadOnlyDictionary<string, string>>();
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