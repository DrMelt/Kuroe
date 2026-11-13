using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>节点在流程树里的路径：段按树层级排列，由流程编译期一次构造，供展示与定位。</summary>
public sealed class NodePathTests
{
    /// <summary>多层容器流程：整体 → 制定计划，整体 → 文档组 → 撰写。</summary>
    private const string ChainFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "制定计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
            { "Name": "文档组", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
              { "Name": "撰写", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } } ]
        }
        """;

    /// <summary>根节点直接是执行节点的流程。</summary>
    private const string RootExecutableFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "直接执行", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] } } ]
        }
        """;

    /// <summary>同一节点组引用两次：两棵实例子树各自拥有同名成员，路径不同。</summary>
    private const string RepeatedInstanceFlow = """
        {
          "Nodes": [
            { "Name": "干", "Kind": "Llm" },
            { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Nodes": [
              { "Label": "实施", "Use": "干", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [ { "Name": "双线", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Label": "甲", "Use": "组", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@目标" } ] },
            { "Label": "乙", "Use": "组", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@目标" } ] }
          ] } } ]
        }
        """;

    [Fact]
    public void Compiled_nodes_carry_tree_path_segmented_by_level()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);

        environment.Submit("补齐 README");
        NodeGraph graph = environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError().Graph;

        Assert.Equal(["整体"],
            graph[NodePath.Of("整体")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "制定计划"],
            graph[NodePath.Of("整体", "制定计划")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组"],
            graph[NodePath.Of("整体", "文档组")].Path.Levels.Select(level => level.Value));
        Assert.Equal(["整体", "文档组", "撰写"],
            graph[NodePath.Of("整体", "文档组", "撰写")].Path.Levels.Select(level => level.Value));
        Assert.Equal("整体/文档组/撰写", graph[NodePath.Of("整体", "文档组", "撰写")].Path.Value);
        Assert.Equal("整体/文档组/撰写", graph[NodePath.Of("整体", "文档组", "撰写")].Path.ToString());
    }

    [Fact]
    public void Root_executable_path_is_itself()
    {
        using TestEnvironment environment = TestEnvironment.Create(RootExecutableFlow);

        environment.Submit("补齐 README");
        NodeGraph graph = environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError().Graph;

        Assert.Equal(["直接执行"],
            graph[NodePath.Of("直接执行")].Path.Levels.Select(level => level.Value));
        Assert.Equal("直接执行", graph[NodePath.Of("直接执行")].Path.Value);
    }

    [Fact]
    public void Container_snapshot_carries_typed_tree_path()
    {
        using TestEnvironment environment = TestEnvironment.Create(ChainFlow);

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot snapshot = environment.Snapshot(id);

        Assert.Equal("整体",
            Assert.Single(snapshot.View.Groups, group => group.Path == "整体").Path);
        Assert.Equal("整体/文档组",
            Assert.Single(snapshot.View.Groups, group => group.Path == "整体/文档组").Path);

        // 同一节点组的两次引用各自拥有同名成员，路径由树位置区分
        using TestEnvironment repeated = TestEnvironment.Create(RepeatedInstanceFlow);
        repeated.Submit("补齐 README", new FlowName("双线"));
        NodeGraph repeatedGraph = repeated.Flows.FindCompiled(new FlowName("双线")).ThrowIfError().Graph;

        Assert.Equal("整体/甲/实施", repeatedGraph.ExecutableNodes[0].Path.Value);
        Assert.Equal("整体/乙/实施", repeatedGraph.ExecutableNodes[1].Path.Value);
        Assert.Equal("实施", repeatedGraph.ExecutableNodes[0].Path.Label.Value);
        NodePath member = NodePath.Of("整体", "甲", "实施");
        Assert.True(repeatedGraph[member].Path.IsUnder(NodePath.Of("整体", "甲")));
        Assert.False(repeatedGraph[member].Path.IsUnder(NodePath.Of("整体", "乙")));
    }
}
