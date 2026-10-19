using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Runtime.Tasks;
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
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
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
        ExecutionReminder reminder = new(environment.Registry);
        foreach (ExecutionSnapshot execution in done.Executables.SelectMany(executable => executable.Executions))
        {
            Assert.Null(reminder.Reminder(environment.Registry.FindExecution(execution.Id).ThrowIfError()));
        }
    }

    private static string Reminder(TestEnvironment environment, ExecutionId id) =>
        Assert.IsType<string>(new ExecutionReminder(environment.Registry).Reminder(environment.Registry.FindExecution(id).ThrowIfError()));
}
