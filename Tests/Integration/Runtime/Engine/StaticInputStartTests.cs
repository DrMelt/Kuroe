using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>容器常量输出：下游经静态输入读取，不依赖运行期产出。</summary>
public sealed class StaticInputStartTests
{
    [Fact]
    public void Container_constant_output_feeds_a_sibling()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerConstantFlow);
        environment.Runner.PortValuesJson = """{ "结论": "完成" }""";

        TaskSnapshot done = environment.Settle(environment.Submit("目标"));

        ExecutableSnapshot use = Assert.Single(done.Executables, node => node.Executable.Label.Value == "使用");
        Assert.Equal("fake", Assert.Single(use.Executions).Input.Texts[new PortName("资料")]);
        Assert.Equal(2, done.Containers.Count);
    }

    private const string ContainerConstantFlow = """
        {
          "Flows": [ { "Name": "默认",
            "Start": { "Outputs": [
              { "Name": "目标", "Type": "Text" },
              { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
            ] },
            "Nodes": [
              { "Name": "外层", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "整体", "Kind": "Container",
                    "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
                    "In": { "MODEL_NAME": "@MODEL_NAME" },
                    "Outputs": [{ "Name": "配置", "Type": "Text", "Mode": "Constant", "Ref": "配置@资料" }],
                    "Nodes": [
                      { "Name": "配置", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                        "Outputs": [{ "Name": "资料", "Type": "Text", "Mode": "Constant", "Ref": "@MODEL_NAME" }] }
                    ] },
                  { "Name": "使用", "Kind": "Llm",
                    "Inputs": [{ "Name": "目标" }, { "Name": "资料", "Type": "Text", "Mode": "Constant" }],
                    "Static": { "MODEL_NAME": "@MODEL_NAME", "资料": "整体@配置" },
                    "From": [ { "目标": "@目标" } ] }
                ] }
            ] } ]
        }
        """;
}
