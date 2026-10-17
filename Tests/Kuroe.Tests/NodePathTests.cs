using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;
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
              { "Name": "制定计划", "Kind": "Llm", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] },
              { "Name": "文档组", "Kind": "Container", "Nodes": [
                { "Name": "撰写", "Kind": "Llm", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>根节点直接是执行节点的流程。</summary>
    private const string RootExecutableFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "实施者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "直接执行", "Kind": "Llm", "Model": "实施者", "From": [ { "目标": "Start@目标" } ] }
          ] } ]
        }
        """;

    /// <summary>同一节点组引用两次：两棵实例子树各自拥有同名成员，路径不同。</summary>
    private const string RepeatedInstanceFlow = """
        {
          "Nodes": [
            { "Name": "干", "Kind": "Llm" },
            { "Name": "组", "Kind": "Container", "Models": ["执行者"], "Inputs": [{ "Name": "计划" }], "Nodes": [
              { "Name": "实施", "Use": "干", "Model": "执行者", "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [ { "Name": "双线", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "甲", "Use": "组", "In": { "计划": "Start@目标" }, "Models": { "执行者": "执行者" } },
              { "Name": "乙", "Use": "组", "In": { "计划": "Start@目标" }, "Models": { "执行者": "执行者" } }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Compiled_nodes_carry_tree_path_segmented_by_level()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);

        TaskId id = environment.Submit("补齐 README");
        NodeGraph graph = environment.Registry.Find(id).ThrowIfError().Graph;

        Assert.Equal([StartNode.ReservedName.Value], graph[NodePath.Of("Start")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体"], graph[NodePath.Of("整体")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "制定计划"], graph[NodePath.Of("整体", "制定计划")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组"], graph[NodePath.Of("整体", "文档组")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组", "撰写"], graph[NodePath.Of("整体", "文档组", "撰写")].Path.Levels.Select(level => level.Value));
        Assert.Equal("整体/文档组/撰写", graph[NodePath.Of("整体", "文档组", "撰写")].Path.Value);
        Assert.Equal("整体/文档组/撰写", graph[NodePath.Of("整体", "文档组", "撰写")].Path.ToString());
    }

    [Fact]
    public void Root_executable_path_is_itself()
    {
        using TestEnvironment environment = TestEnvironment.Create(RootExecutableFlow);

        TaskId id = environment.Submit("补齐 README");
        NodeGraph graph = environment.Registry.Find(id).ThrowIfError().Graph;

        Assert.Equal([StartNode.ReservedName.Value], graph[NodePath.Of("Start")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["直接执行"], graph[NodePath.Of("直接执行")].Path.Levels.Select(level => level.Value));
        Assert.Equal("直接执行", graph[NodePath.Of("直接执行")].Path.Value);
    }

    [Fact]
    public void Container_snapshot_carries_typed_tree_path()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot snapshot = environment.Snapshot(id);

        Assert.Equal("整体", Assert.Single(snapshot.Containers, container => container.Path.Value == "整体").Path.Value);
        Assert.Equal("整体/文档组", Assert.Single(snapshot.Containers, container => container.Path.Value == "整体/文档组").Path.Value);

        // 同一节点组的两次引用各自拥有同名成员，路径由树位置区分
        using TestEnvironment repeated = TestEnvironment.Create(RepeatedInstanceFlow);
        TaskId repeatedId = repeated.Submit("补齐 README", new FlowName("双线"));
        NodeGraph repeatedGraph = repeated.Registry.Find(repeatedId).ThrowIfError().Graph;

        Assert.Equal("整体/甲/实施", repeatedGraph.ExecutableNodes[0].Path.Value);
        Assert.Equal("整体/乙/实施", repeatedGraph.ExecutableNodes[1].Path.Value);
        Assert.Equal("实施", repeatedGraph.ExecutableNodes[0].Path.Name.Value);
        Assert.True(repeatedGraph[NodePath.Of("整体", "甲", "实施")].Path.IsUnder(NodePath.Of("整体", "甲")));
        Assert.False(repeatedGraph[NodePath.Of("整体", "甲", "实施")].Path.IsUnder(NodePath.Of("整体", "乙")));
    }
}
