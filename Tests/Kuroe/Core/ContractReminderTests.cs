using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Node.Llm.Executions;
using Xunit;

namespace Kuroe.Tests;

/// <summary>产出契约的补交提示：声明了输出端口而模型未交回时，点名缺项与对应的契约工具；产出齐备时不给提示。</summary>
public sealed class ContractReminderTests
{
    /// <summary>只声明一个 Text 端口的执行节点，用于验证端口未交回的提示。</summary>
    private const string PortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Missing_port_values_remind_the_port_and_the_contract_tool()
    {
        using TestEnvironment environment = TestEnvironment.Create(PortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        string reminder = Reminder(environment, Assert.Single(blocked.Executables[0].Executions).Id);

        Assert.Contains("「结论」", reminder, StringComparison.Ordinal);
        Assert.Contains("SubmitPortValues", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public void Collected_outputs_give_no_reminder()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ContractReminder reminder = new(new ExecutionStatusReader(environment.Executions));
        foreach (ExecutionSnapshot execution in done.Executables.SelectMany(executable => executable.Executions))
        {
            Assert.Null(reminder.Text(execution.Id));
        }
    }

    /// <summary>Signal 端口未交回时同样点名追问，端口名与交回工具一起给出。</summary>
    [Fact]
    public void Missing_signal_port_reminds_the_port_and_the_contract_tool()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SignalPortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        IExecution moved = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "动手");
        string reminder = Reminder(environment, moved.Id);

        Assert.Contains("「完成」", reminder, StringComparison.Ordinal);
        Assert.Contains("SubmitPortValues", reminder, StringComparison.Ordinal);
    }

    private static string Reminder(TestEnvironment environment, ExecutionId id) =>
        Assert.IsType<string>(new ContractReminder(new ExecutionStatusReader(environment.Executions)).Text(id));
}
