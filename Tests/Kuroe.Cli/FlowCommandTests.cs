using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;

using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>flow 命令对不可用流程的呈现与导入。</summary>
public sealed class FlowCommandTests
{
    [Fact]
    public async Task Flow_list_reports_unavailable_flow()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Input);
        StringWriter output = new();
        StringWriter error = new();

        int code = new FlowCommands(environment.Session, new CliOutput(output, error)).List();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("不可用流程", output.ToString());
        Assert.Contains("Input", output.ToString());
    }

    [Fact]
    public async Task Flow_add_imports_only_compilable_flows()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        await File.WriteAllTextAsync(Path.Combine(environment.Root, "mixed.json"), Mixed);
        StringWriter output = new();
        StringWriter error = new();

        int code = new FlowCommands(environment.Session, new CliOutput(output, error)).Add("mixed.json");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("导入 1 条流程", output.ToString());
        Assert.Contains("未导入", error.ToString());
        Assert.Contains("待输入", error.ToString());
        Assert.Contains(environment.Session.Flows.All(), flow => flow.Name.Value == "可运行");
        Assert.DoesNotContain(environment.Session.Flows.All(), flow => flow.Name.Value == "待输入");
    }

    [Fact]
    public async Task Flow_add_with_only_uncompilable_flows_saves_nothing()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        await File.WriteAllTextAsync(Path.Combine(environment.Root, "input.json"), CliFlows.Input);
        StringWriter output = new();
        StringWriter error = new();

        int code = new FlowCommands(environment.Session, new CliOutput(output, error)).Add("input.json");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("导入 0 条流程", output.ToString());
        Assert.Contains("未导入", error.ToString());
        Assert.DoesNotContain(environment.Session.Flows.All(), flow => flow.Name.Value == "待输入");
    }

    [Fact]
    public async Task Flow_show_prints_the_node_tree()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        StringWriter output = new();
        StringWriter error = new();

        int code = new FlowCommands(environment.Session, new CliOutput(output, error)).Show("默认");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("执行节点", output.ToString());
        Assert.Contains("实施", output.ToString());
        Assert.Empty(error.ToString());
    }

    private const string Mixed = """
        {
          "Flows": [
            {
              "Name": "可运行",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] }
              ]
            },
            {
              "Name": "待输入",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "收话",
                  "Kind": "Input",
                  "Question": "请补充背景",
                  "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                  "From": [ { "目标": "Start@目标" } ]
                }
              ]
            }
          ]
        }
        """;
}