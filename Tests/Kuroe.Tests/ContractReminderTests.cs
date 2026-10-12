using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>产出契约的补交提示：声明了输出端口或条目拆分而模型未交回时，点名缺项与对应的契约工具；产出齐备时不给提示。</summary>
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
                    { "Name": "实施", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
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
        using KuroeHarness harness = KuroeHarness.Create(PortFlow);
        harness.Executor.SubmitsPorts = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        string reminder = Reminder(harness, Assert.Single(blocked.Executables[0].Runs).Id);

        Assert.Contains("「结论」", reminder, StringComparison.Ordinal);
        Assert.Contains("SubmitPortValues", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_items_remind_the_contract_tool()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        string reminder = Reminder(harness, Assert.Single(blocked.Executables[0].Runs).Id);

        Assert.Contains("SubmitWorkItems", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public void Collected_outputs_give_no_reminder()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunReminder reminder = new(harness.Registry);
        foreach (RunSnapshot run in done.Executables.SelectMany(executable => executable.Runs))
        {
            Assert.Null(reminder.Reminder(harness.Registry.FindRun(run.Id).ThrowIfError()));
        }
    }

    private static string Reminder(KuroeHarness harness, RunId id) =>
        Assert.IsType<string>(new RunReminder(harness.Registry).Reminder(harness.Registry.FindRun(id).ThrowIfError()));
}
