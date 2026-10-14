using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Shared.Tests;

/// <summary>上下文模型：run 的叫法、条目序号与出处的可读文本。</summary>
public sealed class ContextModelTests
{
    [Fact]
    public void RunContext_label_merges_step_and_item()
    {
        RunContext stepLevel = Context() with { ItemIndex = null };
        RunContext itemLevel = Context() with { ItemIndex = 2 };

        Assert.Equal("实施", stepLevel.Label);
        Assert.Equal("实施·条目 3", itemLevel.Label);
    }

    [Fact]
    public void RunContext_defaults_execution_count_and_seed()
    {
        RunContext context = Context();

        Assert.Equal(1, context.ExecutionCount);
        Assert.Empty(context.Seed);
    }

    [Fact]
    public void RunSource_points_back_to_its_run()
    {
        RunId run = new(7);
        RunSource source = new(run, new NodeName("实施"));

        Assert.Equal("run #7 · 实施 产出", source.Label);
        Assert.Equal(run, source.FromRun);
    }

    [Fact]
    public void ItemSource_points_back_to_origin()
    {
        RunId plan = new(1);
        ItemSource source = new(plan, 0, "甲");

        Assert.Equal("run #1 · 条目 1：甲", source.Label);
        Assert.Equal(plan, source.FromRun);
    }

    [Fact]
    public void Input_and_start_sources_name_their_origin()
    {
        InputSource input = new(new NodeName("取舍"));
        StartSource start = new(new PortName("目标"));

        Assert.Equal("节点「取舍」的输入", input.Label);
        Assert.Equal("流程起点端口「目标」", start.Label);
        Assert.Null(input.FromRun);
        Assert.Null(start.FromRun);
    }

    [Fact]
    public void Container_and_frame_sources_point_back_to_their_run()
    {
        RunId run = new(4);
        ContainerPortSource container = new(new NodeName("文档组"), new PortName("结论"));
        ContextFrameSource step = new(run, new NodeName("实施"), null);
        ContextFrameSource item = new(run, new NodeName("实施"), 1);

        Assert.Equal("容器「文档组」端口「结论」", container.Label);
        Assert.Null(container.FromRun);
        Assert.Equal("run #4 · 实施 的上下文", step.Label);
        Assert.Equal("run #4 · 实施 条目 2 的上下文", item.Label);
        Assert.Equal(run, item.FromRun);
    }

    private static RunContext Context() => new()
    {
        Task = new TaskId(1),
        NodeIndex = 1,
        NodeName = new NodeName("实施"),
        Instruction = "做事",
    };
}
