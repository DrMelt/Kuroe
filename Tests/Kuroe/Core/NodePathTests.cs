using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>节点在流程树里的路径：段按树层级排列，由流程编译期一次构造，供展示与定位。</summary>
public sealed class NodePathTests
{
    /// <summary>多层容器流程：整体 → 制定计划，整体 → 文档组 → 撰写。</summary>
    private const string ChainFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Name": "文档组", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                { "Name": "撰写", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>根节点直接是执行节点的流程。</summary>
    private const string RootExecutableFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "直接执行", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] }
          ] } ]
        }
        """;

    /// <summary>同一节点组引用两次：两棵实例子树各自拥有同名成员，路径不同。</summary>
    private const string RepeatedInstanceFlow = """
        {
          "Nodes": [
            { "Name": "干", "Kind": "Llm" },
            { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Label": "实施", "Use": "干", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [ { "Name": "双线", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "甲", "Use": "组", "In": { "计划": "@目标", "MODEL_NAME": "@MODEL_NAME" } },
              { "Label": "乙", "Use": "组", "In": { "计划": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Compiled_nodes_carry_tree_path_segmented_by_level()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);

        TaskId id = environment.Submit("补齐 README");
        INodeGraph graph = environment.Registry.Find(id).ThrowIfError().Context.Graph;

        Assert.Equal([StartNode.ReservedLabel.Value], graph[NodePath.Of("Start")].Path.Levels.Select(level => level.Value));
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
        INodeGraph graph = environment.Registry.Find(id).ThrowIfError().Context.Graph;

        Assert.Equal([StartNode.ReservedLabel.Value], graph[NodePath.Of("Start")].Path.Levels.Select(level => level.Value));
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
        INodeGraph repeatedGraph = repeated.Registry.Find(repeatedId).ThrowIfError().Context.Graph;

        Assert.Equal("整体/甲/实施", repeatedGraph.ExecutableNodes[0].Path.Value);
        Assert.Equal("整体/乙/实施", repeatedGraph.ExecutableNodes[1].Path.Value);
        Assert.Equal("实施", repeatedGraph.ExecutableNodes[0].Path.Label.Value);
        Assert.True(repeatedGraph[NodePath.Of("整体", "甲", "实施")].Path.IsUnder(NodePath.Of("整体", "甲")));
        Assert.False(repeatedGraph[NodePath.Of("整体", "甲", "实施")].Path.IsUnder(NodePath.Of("整体", "乙")));
    }
}
