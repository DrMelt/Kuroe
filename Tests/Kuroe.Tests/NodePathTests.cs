using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.TestSupport;
using Kuroe.Flows.StaticGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>节点在流程树里的路径：段按树层级排列，由流程编译期一次构造，供展示与定位。</summary>
public sealed class NodePathTests
{
    /// <summary>多层容器流程：整体 → 制定计划，整体 → 文档组 → 撰写。</summary>
    private const string ChainFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "实施者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "制定计划", "Kind": "Executable", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] },
              { "Name": "文档组", "Kind": "Container", "Nodes": [
                { "Name": "撰写", "Kind": "Executable", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>根节点直接是执行节点的流程。</summary>
    private const string RootExecutableFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "实施者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "直接执行", "Kind": "Executable", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] }
          ] } ]
        }
        """;

    [Fact]
    public void Compiled_nodes_carry_tree_path_segmented_by_level()
    {
        using KuroeHarness harness = KuroeHarness.Create(ChainFlow);

        TaskId id = harness.Submit("补齐 README");
        NodeGraph graph = harness.Registry.Find(id).ThrowIfError().Graph;

        Assert.Equal([StartNode.ReservedName.Value], graph[0].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体"], graph[1].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "制定计划"], graph[2].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组"], graph[3].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组", "撰写"], graph[4].Path.Levels.Select(level => level.Value));
        Assert.Equal("整体/文档组/撰写", graph[4].Path.Value);
        Assert.Equal("整体/文档组/撰写", graph[4].Path.ToString());
    }

    [Fact]
    public void Root_executable_path_is_itself()
    {
        using KuroeHarness harness = KuroeHarness.Create(RootExecutableFlow);

        TaskId id = harness.Submit("补齐 README");
        NodeGraph graph = harness.Registry.Find(id).ThrowIfError().Graph;

        Assert.Equal([StartNode.ReservedName.Value], graph[0].Path.Levels.Select(level => level.Value));
        Assert.Equal(["直接执行"], graph[1].Path.Levels.Select(level => level.Value));
        Assert.Equal("直接执行", graph[1].Path.Value);
    }

    [Fact]
    public void Container_snapshot_carries_typed_tree_path()
    {
        using KuroeHarness harness = KuroeHarness.Create(ChainFlow);

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot snapshot = harness.Snapshot(id);

        Assert.Equal("整体", Assert.Single(snapshot.Containers, container => container.Name == "整体").Path.Value);
        Assert.Equal("整体/文档组", Assert.Single(snapshot.Containers, container => container.Name == "文档组").Path.Value);
    }

    [Fact]
    public void Paths_with_same_segments_are_equal_regardless_of_collection_instance()
    {
        NodePath first = new([new NodeName("整体"), new NodeName("文档组"), new NodeName("撰写")]);
        NodePath second = new([new NodeName("整体"), new NodeName("文档组"), new NodeName("撰写")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Paths_with_different_segments_are_not_equal()
    {
        NodePath shallow = new([new NodeName("整体")]);
        NodePath deep = new([new NodeName("整体"), new NodeName("文档组")]);

        Assert.NotEqual(shallow, deep);
    }
}
