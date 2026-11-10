using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;

using Xunit;

namespace Kuroe.Cli.Tests.Commands;

/// <summary>flow 命令对不可用流程的呈现与导入。</summary>
public sealed class FlowCommandsTests
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
        await File.WriteAllTextAsync(Path.Combine(environment.Root, "mixed.json"), Mixed, TestContext.Current.CancellationToken);
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
        await File.WriteAllTextAsync(Path.Combine(environment.Root, "input.json"), CliFlows.Input, TestContext.Current.CancellationToken);
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

    /// <summary>开始端口的宿主输入名取自宿主开始输入表。</summary>
    [Fact]
    public async Task Flow_show_prints_host_start_inputs_from_the_form()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.HostInput);
        StringWriter output = new();
        StringWriter error = new();

        int code = new FlowCommands(environment.Session, new CliOutput(output, error)).Show("默认");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("启动路径←WORKSPACE_PATH", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    private const string Mixed = """
        {
          "Flows": [
            {
              "Name": "可运行",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": { "Name": "实施", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            },
            {
              "Name": "待输入",
              "Ports": [{ "Name": "目标", "Type": "Text" }],
              "Root": {
                "Name": "收话",
                "Kind": "Input",
                "Question": "请补充背景",
                "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                "From": [ { "目标": "@目标" } ]
              }
            }
          ]
        }
        """;
}