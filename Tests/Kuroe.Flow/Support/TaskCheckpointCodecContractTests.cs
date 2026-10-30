using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.TestSupport;

/// <summary>检查点编解码契约：往返、节点多态、空值、出处、错误与稳定输出。</summary>
public abstract class TaskCheckpointCodecContractTests
{
    protected abstract ITaskCheckpointCodec CreateCodec();

    [Fact]
    public void Round_trip_preserves_task_checkpoint()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        TaskCheckpointDocument loaded = codec.Read(codec.Write(document).ThrowIfError()).ThrowIfError();

        Assert.Equal(document.Version, loaded.Version);
        Assert.Equal(document.Task.Identity, loaded.Task.Identity);
        Assert.Equal(document.Task.Flow, loaded.Task.Flow);
        Assert.Equal(document.Task.Lifecycle, loaded.Task.Lifecycle);
        Assert.Equal(document.Task.Completed, loaded.Task.Completed);
        Assert.Equal(document.Task.Executions.Count, loaded.Task.Executions.Count);
        Assert.Equal(document.Task.Nodes.Count, loaded.Task.Nodes.Count);
        Assert.Equal(document.Task.Pending.Targets, loaded.Task.Pending.Targets);
    }

    [Fact]
    public void Round_trip_preserves_all_node_checkpoint_kinds()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        TaskCheckpointDocument loaded = codec.Read(codec.Write(document).ThrowIfError()).ThrowIfError();

        Assert.Single(loaded.Task.Nodes.OfType<ExecutableNodeCheckpoint>());
        Assert.Single(loaded.Task.Nodes.OfType<SelfDrivenNodeCheckpoint>());
        Assert.Single(loaded.Task.Nodes.OfType<InputNodeCheckpoint>());
        Assert.Single(loaded.Task.Nodes.OfType<ApprovalNodeCheckpoint>());
        Assert.Single(loaded.Task.Nodes.OfType<ContainerNodeCheckpoint>());
    }

    [Fact]
    public void Round_trip_preserves_nullable_fields()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        TaskCheckpoint loaded = codec.Read(codec.Write(document).ThrowIfError()).ThrowIfError().Task;

        Assert.Equal(document.Task.Executions[1].StartedAt, loaded.Executions[1].StartedAt);
        Assert.Null(Assert.IsType<SelfDrivenNodeCheckpoint>(loaded.Nodes[1]).Content);
        Assert.Null(loaded.Nodes[1].Common.LastInputGroup);
    }

    [Fact]
    public void Round_trip_preserves_context_source_kinds()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = WithAllContextSources(TaskCheckpointFixture.CreateDocument());

        TaskCheckpoint loaded = codec.Read(codec.Write(document).ThrowIfError()).ThrowIfError().Task;

        Assert.Equal(
            Enum.GetValues<ContextSourceKind>(),
            [.. loaded.Executions[0].Inputs.Select(value => value.Origin.Kind)]);
    }

    [Fact]
    public void Read_rejects_malformed_json()
    {
        ITaskCheckpointCodec codec = CreateCodec();

        Assert.True(codec.Read("{").IsError);
    }

    [Fact]
    public void Read_rejects_unknown_version()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument() with { Version = 999 };

        Assert.True(codec.Read(codec.Write(document).ThrowIfError()).IsError);
    }

    [Fact]
    public void Write_is_deterministic()
    {
        ITaskCheckpointCodec codec = CreateCodec();
        TaskCheckpointDocument document = TaskCheckpointFixture.CreateDocument();

        Assert.Equal(codec.Write(document).ThrowIfError(), codec.Write(document).ThrowIfError());
    }

    private static TaskCheckpointDocument WithAllContextSources(TaskCheckpointDocument document)
    {
        NodePath node = NodePath.Of("整体", "计划");
        PortName port = new("结论");
        ExecutionId execution = new(11);
        TaskCheckpoint task = document.Task;
        ExecutionCheckpoint first = task.Executions[0] with
        {
            Inputs =
            [
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Execution, execution, node, port), "执行"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Input, null, node, port), "输入"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Start, null, null, port), "开始"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.ContainerPort, null, NodePath.Of("整体"), port), "容器"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Default, null, node, port), "默认"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Constant, null, node, port), "常量"),
                new InputValueCheckpoint(port, new ContextSourceCheckpoint(ContextSourceKind.Node, null, node, port), "节点"),
            ],
        };

        return document with
        {
            Task = task with { Executions = [first, .. task.Executions.Skip(1)] },
        };
    }
}
