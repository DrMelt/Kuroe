using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>节点库引用在运行时展开为独立实例。</summary>
public sealed class NodeLibraryRuntimeTests
{
    [Fact(DisplayName = "同一库容器实例化两次时成员互不混淆")]
    public void Same_library_container_instantiated_twice_keeps_members_distinct()
    {
        using TestEnvironment environment = TestEnvironment.Create(DualInstanceFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README", new FlowName("双线")));

        Assert.Equal(TaskState.Done, done.State);
    }

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
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
              { "Label": "制定A", "Use": "规划", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Label": "制定B", "Use": "规划", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Label": "交付A", "Use": "交付", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定A@计划" } ] },
              { "Label": "交付B", "Use": "交付", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定B@计划" } ] }
                ]
              }
            }
          ]
        }
        """;
}
