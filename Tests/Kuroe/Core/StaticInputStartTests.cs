using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>静态输入启动与常量输出：全静态输入的节点正常执行，不依赖 From；常量输出只由下游 Static 读取。</summary>
public sealed class StaticInputStartTests
{
    [Fact]
    public void Static_input_start_executes_and_constant_output_feeds_static()
    {
        using TestEnvironment environment = TestEnvironment.Create(FromFlow);
        environment.Runner.PortValuesJson = """{ "结论": "完成" }""";

        TaskSnapshot done = environment.Settle(environment.Submit("目标"));

        ExecutableSnapshot config = Assert.Single(done.Executables, node => node.Executable.Label.Value == "配置");
        Assert.Equal(NodeState.Done, Assert.Single(done.ExecutableStates, state => state.Node.Label == config.Executable.Label).State);
        Assert.Single(config.Executions);
        Assert.Equal("fake", config.ConstantTexts.Text(new PortName("资料")));

        ExecutableSnapshot use = Assert.Single(done.Executables, node => node.Executable.Label.Value == "使用");
        Assert.Equal("fake", Assert.Single(use.Executions).Input.Texts[new PortName("资料")]);

        ContainerSnapshot container = Assert.Single(done.Containers);
        Assert.Contains(container.Children, path => path.Label == config.Executable.Label);
        Assert.Contains(container.Children, path => path.Label == use.Executable.Label);
    }

    [Fact]
    public void Static_input_can_read_a_constant_output_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(StaticFlow);
        environment.Runner.PortValuesJson = """{ "结论": "完成" }""";

        TaskSnapshot done = environment.Settle(environment.Submit("目标"));

        ExecutableSnapshot config = Assert.Single(done.Executables, node => node.Executable.Label.Value == "配置");
        Assert.Single(config.Executions);

        ExecutableSnapshot use = Assert.Single(done.Executables, node => node.Executable.Label.Value == "使用");
        Assert.Equal("fake", Assert.Single(use.Executions).Input.Texts[new PortName("资料")]);
    }

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

    private const string FromFlow = """
        {
          "Flows": [ { "Name": "默认",
            "Start": { "Outputs": [
              { "Name": "目标", "Type": "Text" },
              { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
            ] },
            "Nodes": [
              { "Name": "整体", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "配置", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                    "Outputs": [{ "Name": "资料", "Type": "Text", "Constant": true, "Ref": "@MODEL_NAME" }] },
                  { "Name": "使用", "Kind": "Llm",
                    "Inputs": [{ "Name": "目标" }, { "Name": "资料", "Type": "Text", "Static": true }],
                    "Static": { "MODEL_NAME": "@MODEL_NAME", "资料": "配置@资料" },
                    "From": [ { "目标": "@目标" } ] }
                ] }
            ] } ]
        }
        """;

    private const string ContainerConstantFlow = """
        {
          "Flows": [ { "Name": "默认",
            "Start": { "Outputs": [
              { "Name": "目标", "Type": "Text" },
              { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
            ] },
            "Nodes": [
              { "Name": "外层", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "整体", "Kind": "Container",
                    "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                    "In": { "MODEL_NAME": "@MODEL_NAME" },
                    "Outputs": [{ "Name": "配置", "Type": "Text", "Constant": true, "Ref": "配置@资料" }],
                    "Nodes": [
                      { "Name": "配置", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                        "Outputs": [{ "Name": "资料", "Type": "Text", "Constant": true, "Ref": "@MODEL_NAME" }] }
                    ] },
                  { "Name": "使用", "Kind": "Llm",
                    "Inputs": [{ "Name": "目标" }, { "Name": "资料", "Type": "Text", "Static": true }],
                    "Static": { "MODEL_NAME": "@MODEL_NAME", "资料": "整体@配置" },
                    "From": [ { "目标": "@目标" } ] }
                ] }
            ] } ]
        }
        """;

    private const string StaticFlow = """
        {
          "Flows": [ { "Name": "默认",
            "Start": { "Outputs": [
              { "Name": "目标", "Type": "Text" },
              { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
            ] },
            "Nodes": [
              { "Name": "整体", "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "配置", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                    "Outputs": [{ "Name": "资料", "Type": "Text", "Constant": true, "Ref": "@MODEL_NAME" }] },
                  { "Name": "使用", "Kind": "Llm",
                    "Inputs": [{ "Name": "目标" }, { "Name": "资料", "Type": "Text", "Static": true }],
                    "Static": { "MODEL_NAME": "@MODEL_NAME", "资料": "配置@资料" },
                    "From": [ { "目标": "@目标" } ] }
                ] }
            ] } ]
        }
        """;
}
