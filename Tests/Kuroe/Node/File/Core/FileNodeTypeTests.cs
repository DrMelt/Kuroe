using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Node.File.Tests;

/// <summary>文件节点类型的形状与类型规则：不启动执行，按版本读写工作目录内文件。</summary>
public sealed class FileNodeTypeTests
{
    private static readonly FileParameters Parameters = new()
    {
        Path = "{root}/a.txt",
        Variables = new Dictionary<string, string> { ["root"] = "任务路径" },
        RootDirectory = Path.GetFullPath("."),
        MaxChars = 8000,
    };

    private static readonly FileReadNodeType ReadType = new(Parameters);
    private static readonly FileWriteNodeType WriteType = new(Parameters);

    [Fact]
    public void Names_are_the_kinds_written_in_files()
    {
        Assert.Equal("FileRead", FileReadNodeTypes.Name.Value);
        Assert.Equal("FileWrite", FileWriteNodeTypes.Name.Value);
        Assert.Equal(FileReadNodeTypes.Name, ReadType.Kind);
        Assert.Equal(FileWriteNodeTypes.Name, WriteType.Kind);
    }

    [Fact]
    public void Create_keeps_the_declaration_on_the_instance()
    {
        INodeType read = FileReadNodeTypes.Create(Parameters);
        INodeType write = FileWriteNodeTypes.Create(Parameters);

        Assert.Same(Parameters, Assert.IsType<FileReadNodeType>(read).Parameters);
        Assert.Same(Parameters, Assert.IsType<FileWriteNodeType>(write).Parameters);
    }

    [Fact]
    public void Default_limits_are_8000_for_read_and_unlimited_for_write()
    {
        Assert.Equal(8000, FileReadNodeTypes.DefaultMaxChars);
        Assert.Equal(0, FileWriteNodeTypes.DefaultMaxChars);
    }

    [Fact]
    public void Read_shape_takes_text_and_signal_inputs_and_produces_text()
    {
        NodeTypeShape shape = ReadType.Shape;

        Assert.False(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text], shape.OutputTypes);
    }

    [Fact]
    public void Write_shape_takes_text_and_signal_inputs_and_may_produce_text_or_signal()
    {
        NodeTypeShape shape = WriteType.Shape;

        Assert.False(shape.DispatchesExecutions);
        Assert.False(shape.Organizes);
        Assert.False(shape.StartsUnaided);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.InputTypes);
        Assert.Equal([PortTypes.Text, PortTypes.Signal], shape.OutputTypes);
    }

    [Fact]
    public void Plain_read_node_passes() => Assert.Empty(Failures(ReadType, ReadNode()));

    [Fact]
    public void Plain_write_node_passes() => Assert.Empty(Failures(WriteType, WriteNode()));

    [Fact]
    public void Read_rejects_execution_config()
    {
        NodeSpec node = ReadNode() with
        {
            Model = new ModelRef("执行者"),
            Spec = new ExecutableSpec { Validate = new OutputValidation(ValidationPredicate.NonEmpty, null) },
        };

        string[] errors = [.. Failures(ReadType, node)];

        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("文件读取节点不启动执行，不能声明模型选择。"));
        Assert.Contains(errors, error => error.EndsWith("文件读取节点不启动执行，不能声明 Validate。"));
    }

    [Fact]
    public void Write_rejects_execution_config()
    {
        NodeSpec node = WriteNode() with
        {
            Model = new ModelRef("执行者"),
            Spec = new ExecutableSpec { Validate = new OutputValidation(ValidationPredicate.NonEmpty, null) },
        };

        string[] errors = [.. Failures(WriteType, node)];

        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, error => error.EndsWith("文件写入节点不启动执行，不能声明模型选择。"));
        Assert.Contains(errors, error => error.EndsWith("文件写入节点不启动执行，不能声明 Validate。"));
    }

    [Fact]
    public void Read_requires_one_trigger_input_and_exactly_one_output()
    {
        string[] errors = [.. Failures(ReadType, ReadNode() with { Inputs = [], Outputs = [] })];

        Assert.Contains(errors, error => error.EndsWith("文件读取节点必须声明至少一个 Signal 输入端口作为触发。"));
        Assert.Contains(errors, error => error.EndsWith("路径变量 root 映射到未声明的输入端口 任务路径。"));
        Assert.Contains(errors, error => error.EndsWith("文件读取节点必须声明一个输出端口，读取内容写入该端口。"));
    }

    [Fact]
    public void Read_rejects_text_inputs_outside_path_variables()
    {
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
            new(new PortName("正文"), PortTypes.Text),
        ];

        Assert.Contains(Failures(ReadType, ReadNode(inputs)),
            error => error.EndsWith("文件读取节点的 Text 输入必须是路径变量端口。"));
    }

    [Fact]
    public void Read_rejects_a_signal_variable_port()
    {
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Signal),
            new(new PortName("触发"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(ReadType, ReadNode(inputs)),
            error => error.EndsWith("路径变量 root 映射到的输入端口 任务路径 必须是 Text。"));
    }

    [Fact]
    public void Read_rejects_more_than_one_output()
    {
        Port[] outputs =
        [
            new(new PortName("甲"), PortTypes.Text),
            new(new PortName("乙"), PortTypes.Text),
        ];

        Assert.Contains(Failures(ReadType, ReadNode(outputs: outputs)),
            error => error.EndsWith("文件读取节点必须声明一个输出端口，读取内容写入该端口。"));
    }

    [Fact]
    public void Write_requires_exactly_one_text_input()
    {
        InputPort[] inputs =
        [
            new(new PortName("正文"), PortTypes.Text),
            new(new PortName("补充"), PortTypes.Text),
            new(new PortName("任务路径"), PortTypes.Text),
        ];

        Assert.Contains(Failures(WriteType, WriteNode(inputs)),
            error => error.EndsWith("文件写入节点必须声明一个 Text 输入端口，写入内容取自它。"));
    }

    [Fact]
    public void Write_requires_the_variable_port()
    {
        InputPort[] inputs = [new(new PortName("正文"), PortTypes.Text)];

        Assert.Contains(Failures(WriteType, WriteNode(inputs)),
            error => error.EndsWith("路径变量 root 映射到未声明的输入端口 任务路径。"));
    }

    [Fact]
    public void Write_accepts_signal_inputs_alongside_the_text_input()
    {
        InputPort[] inputs =
        [
            new(new PortName("正文"), PortTypes.Text),
            new(new PortName("任务路径"), PortTypes.Text),
            new(new PortName("批准"), PortTypes.Signal),
        ];

        Assert.Empty(Failures(WriteType, WriteNode(inputs)));
    }

    [Fact]
    public void Write_rejects_more_than_one_output()
    {
        Port[] outputs =
        [
            new(new PortName("甲"), PortTypes.Text),
            new(new PortName("乙"), PortTypes.Signal),
        ];

        Assert.Contains(Failures(WriteType, WriteNode(outputs: outputs)),
            error => error.EndsWith("文件写入节点最多声明一个输出端口。"));
    }

    [Fact]
    public void MaxExecutions_must_be_positive()
    {
        NodeSpec read = ReadNode() with { Spec = new ExecutableSpec { MaxExecutions = 0 } };
        NodeSpec write = WriteNode() with { Spec = new ExecutableSpec { MaxExecutions = 0 } };

        Assert.Contains(Failures(ReadType, read), error => error.EndsWith("MaxExecutions 必须是正整数。"));
        Assert.Contains(Failures(WriteType, write), error => error.EndsWith("MaxExecutions 必须是正整数。"));
    }

    private static IReadOnlyList<string> Failures(INodeType type, NodeSpec node)
    {
        List<Error> errors = [];
        type.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static NodeSpec ReadNode(InputPort[]? inputs = null, Port[]? outputs = null) => new()
    {
        Label = new NodeLabel("读入"),
        Type = ReadType,
        Spec = new ExecutableSpec(),
        Inputs = inputs
            ?? [
                new InputPort(new PortName("任务路径"), PortTypes.Text),
                new InputPort(new PortName("触发"), PortTypes.Signal),
            ],
        Outputs = outputs ?? [new Port(new PortName("正文"), PortTypes.Text)],
    };

    private static NodeSpec WriteNode(InputPort[]? inputs = null, Port[]? outputs = null) => new()
    {
        Label = new NodeLabel("写出"),
        Type = WriteType,
        Spec = new ExecutableSpec(),
        Inputs = inputs
            ?? [
                new InputPort(new PortName("正文"), PortTypes.Text),
                new InputPort(new PortName("任务路径"), PortTypes.Text),
            ],
        Outputs = outputs ?? [],
    };
}