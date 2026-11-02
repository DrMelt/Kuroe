using Kuroe.Configuration.Files;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>节点库引用在运行时展开为独立实例。</summary>
public sealed class NodeLibraryRuntimeTests
{
    [Fact]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using TestEnvironment environment = TestEnvironment.Create(DualInstanceFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README", new FlowName("双线")));

        Assert.Equal(TaskState.Done, done.State);
    }

    /// <summary>文件定义自定义节点时，流程仍可引用宿主提供的内置容器；落盘只写文件来源的节点定义。</summary>
    [Fact]
    public void Builtin_definition_stays_usable_and_unwritten_when_file_defines_its_own()
    {
        using TestEnvironment environment = TestEnvironment.Create(BuiltinReferenceFlow);

        NodeSpec root = environment.Flows.Find(new FlowName("内置引用")).ThrowIfError().RootNode;
        Assert.NotNull(root.Nodes);
        NodeSpec container = Assert.Single(root.Nodes, node => node.Label == new NodeLabel("交付"));
        Assert.NotNull(container.Nodes);
        Assert.Single(container.Nodes, node => node.Label == new NodeLabel("实施"));

        IReadOnlyList<NodeDefinitionDocument> nodes = new JsonNodeFormat()
            .Read(File.ReadAllText(KuroePaths.At(environment.Root).NodesFile)).ThrowIfError();
        IReadOnlyList<NodeLabel> savedNodes = [.. nodes.Select(node => new NodeLabel(node.Name))];
        Assert.Equal([new NodeLabel("准备")], savedNodes);
    }

    /// <summary>文件只定义自己的节点，流程同时引用内置容器。</summary>
    private const string BuiltinReferenceFlow = """
        {
          "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Prompt": "写出计划", "Outputs": [{ "Name": "计划", "Type": "Text" }] }
          ],
          "Flows": [
            {
              "Name": "内置引用",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Label": "定计划", "Use": "准备", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
                  { "Label": "交付", "Use": "交付", "Bindings": { "计划": "定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>同一库容器引用两次：两棵实例子树各自保留成员。</summary>
    private const string DualInstanceFlow = """
        {
          "Nodes": [
            { "Name": "规划", "Kind": "Llm", "Outputs": [{ "Name": "计划", "Type": "Text" }] },
            { "Name": "执行", "Kind": "Llm" },
            { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Nodes": [
              { "Label": "实施", "Use": "执行", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
            ] }
          ],
          "Flows": [
            {
              "Name": "双线",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "制定A", "Use": "规划", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Label": "制定B", "Use": "规划", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Label": "交付A", "Use": "交付", "Bindings": { "计划": "制定A@计划", "MODEL_NAME": "@MODEL_NAME" } },
              { "Label": "交付B", "Use": "交付", "Bindings": { "计划": "制定B@计划", "MODEL_NAME": "@MODEL_NAME" } }
                ]
              }
            }
          ]
        }
        """;

}
