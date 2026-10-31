using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>单节点最高执行次数：路径达上限后不再启动新执行，未成功的停在阻塞且返工不放行。</summary>
public sealed class ExecutionLimitTests
{
    /// <summary>执行上限到点后不再重跑，未成功的停在阻塞且返工不放行。</summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("uncollected")]
    [InlineData("validation")]
    public void Exhausted_limit_blocks_and_rejects_rework(string reason)
    {
        string flow = reason switch
        {
            "uncollected" => SingleNodeLimitFlow,
            "validation" => ValidationFlow,
            _ => FailingNodeFlow,
        };

        using TestEnvironment environment = TestEnvironment.Create(flow);
        switch (reason)
        {
            case "failed":
                environment.Runner.FailsWhen = _ => true;
                break;
            case "uncollected":
                environment.Runner.SubmitsPorts = false;
                break;
            default:
                environment.Runner.Reply = _ => "产出不达标";
                break;
        }

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, Executions(task, "干活"));
        Assert.True(environment.Tasks.Rework(id).IsError, "达到上限的节点不能再返工");

        if (reason == "failed")
        {
            Assert.Contains(blocked.Journal,
                entry => entry.Level == JournalLevel.Error && entry.Text.Contains("已达上限"));
        }
    }

    /// <summary>容器统一上限到点后，组内失败的执行节点停驻且返工不放行。</summary>
    [Fact]
    public void Container_limit_blocks_failed_member_at_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleExecutionContainerFlow);
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, Executions(task, "干活"));
        Assert.True(environment.Tasks.Rework(id).IsError, "达到上限的节点不能再返工");
    }

    /// <summary>引用容器时覆盖库定义统一上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_container_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes
            .Single(node => node.Path.Value == "整体/较重/干活").MaxExecutions);
        Assert.Equal(3, task.Context.Graph.ExecutableNodes
            .Single(node => node.Path.Value == "整体/较轻/干活").MaxExecutions);
    }

    /// <summary>引用库执行定义时覆盖库上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_library_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes
            .Single(node => node.Label.Value == "覆盖干活").MaxExecutions);
        Assert.Equal(3, task.Context.Graph.ExecutableNodes
            .Single(node => node.Label.Value == "继承干活").MaxExecutions);
    }

    /// <summary>环迭代受上限约束：有环外终止时停在新版本之前，无终止时全部节点迭代到上限。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Loop_iteration_stops_at_the_limit(bool terminates)
    {
        using TestEnvironment environment = TestEnvironment.Create(terminates ? LoopWithLimitFlow : LoopWithoutLimitFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value == "审查" ? "审查通过" : "产出";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "审查" => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        int expected = terminates ? 1 : 5;
        Assert.Equal(expected, done.Executables.Single(node => node.Executable.Label == new NodeLabel("修订")).Executions.Count);
        Assert.Equal(expected, done.Executables.Single(node => node.Executable.Label == new NodeLabel("交付")).Executions.Count);
        if (!terminates)
        {
            Assert.Equal(5, done.Executables.Single(node => node.Executable.Label == new NodeLabel("审查")).Executions.Count);
        }
    }

    /// <summary>恒失败的单个节点，满 2 次后停在阻塞。</summary>
    private const string FailingNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 2, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>声明输出端口却不交回的单个节点，限两次执行。</summary>
    private const string SingleNodeLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 2, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>校验永远不通过的节点，重复到上限后停在阻塞。</summary>
    private const string ValidationFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 2, "From": [ { "目标": "@目标" } ],
                "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>修订被计划与审查交替驱动，执行满 1 次即达上限，不再被审查新版本重启。</summary>
    private const string LoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "修订", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 1, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>无环外终止的互驱动环：修订被计划与审查交替驱动，全部节点迭代到显式上限后任务收敛为完成。</summary>
    private const string LoopWithoutLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "修订", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 5, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 5, "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 5, "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>容器统一上限 1 次，组内节点失败后停驻。</summary>
    private const string SingleExecutionContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxExecutions": 1, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库容器定义声明统一上限，引用处可覆盖或继承。</summary>
    private const string ReferenceContainerLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活组", "Kind": "Container", "MaxExecutions": 3, "Inputs": [{ "Name": "入口" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "入口": "@入口" } ] }
            ] }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "较重", "Use": "干活组", "MaxExecutions": 5, "In": { "入口": "@目标", "MODEL_NAME": "@MODEL_NAME" } },
              { "Label": "较轻", "Use": "干活组", "In": { "入口": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义声明上限，引用处可覆盖或继承。</summary>
    private const string LibraryLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活库", "Kind": "Llm", "Tools": [], "MaxExecutions": 3 }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "覆盖干活", "Use": "干活库", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 5, "From": [ { "目标": "@目标" } ] },
              { "Label": "继承干活", "Use": "干活库", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>执行节点按启动的执行记录计数。</summary>
    private static int Executions(WorkTask task, string label) =>
        task.Snapshot().Executables
            .Single(executable => executable.Executable.Path == task.Context.Graph.PathOf(label))
            .Executions.Count;
}
