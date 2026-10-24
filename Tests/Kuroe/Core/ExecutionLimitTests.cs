using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>单节点最高执行次数：路径达上限后不再启动新执行，未成功的停在阻塞且返工不放行。</summary>
public sealed class ExecutionLimitTests
{
    [Fact]
    public void Failed_node_exhausts_limit_and_rejects_rework()
    {
        using TestEnvironment environment = TestEnvironment.Create(FailingNodeFlow);
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, Executions(task, "干活"));
        Assert.Contains(blocked.Journal, entry => entry is ErrorEntry error && error.Text.Contains("已达上限"));
        Assert.True(environment.Tasks.Rework(id).IsError, "达到上限的节点不能再返工");
    }

    /// <summary>声明输出端口却不交回，未收口重复到上限后停在阻塞。</summary>
    [Fact]
    public void Uncollected_output_exhausts_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeLimitFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, Executions(task, "干活"));
        Assert.True(environment.Tasks.Rework(id).IsError);
    }

    [Fact]
    public void Validation_failure_exhausts_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ValidationFlow);
        environment.Runner.Reply = _ => "产出不达标";

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, Executions(task, "干活"));
        Assert.True(environment.Tasks.Rework(id).IsError);
    }

    /// <summary>环迭代触发的新来源版本被上限拦截：上限低于自然停止点时，修订不再被审查新版本重启。</summary>
    [Fact]
    public void Loop_stops_restarting_at_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopWithLimitFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value == "审查" ? "审查通过" : "产出";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "审查" => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(node => node.Executable.Label == new NodeLabel("修订")).Executions);
        Assert.Single(done.Executables.Single(node => node.Executable.Label == new NodeLabel("交付")).Executions);
    }

    /// <summary>互驱动环迭代到每节点的执行上限后收敛为完成，验证执行上限约束生效。</summary>
    [Fact]
    public void Loop_iterates_to_max_executions()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopWithoutLimitFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value == "审查" ? "审查通过" : "产出";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "审查" => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Label == new NodeLabel("修订")).Executions.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Label == new NodeLabel("审查")).Executions.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Label == new NodeLabel("交付")).Executions.Count);
    }

    /// <summary>恒失败的单个节点，满 2 次后停在阻塞。</summary>
    private const string FailingNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "MaxExecutions": 2, "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>声明输出端口却不交回的单个节点，限两次执行。</summary>
    private const string SingleNodeLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "MaxExecutions": 2, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>校验永远不通过的节点，重复到上限后停在阻塞。</summary>
    private const string ValidationFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 2, "From": [ { "目标": "Start@目标" } ],
                "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>修订被计划与审查交替驱动，执行满 1 次即达上限，不再被审查新版本重启。</summary>
    private const string LoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 1, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>无环外终止的互驱动环：修订被计划与审查交替驱动，全部节点迭代到显式上限后任务收敛为完成。</summary>
    private const string LoopWithoutLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 5, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 5, "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Model": "执行者", "MaxExecutions": 5, "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string DefaultLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义声明上限，引用侧可覆盖，未覆盖时继承库定义。</summary>
    private const string LibraryLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活库", "Kind": "Llm", "Tools": [], "MaxExecutions": 3 }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Label": "覆盖干活", "Use": "干活库", "Model": "执行者", "MaxExecutions": 5, "From": [ { "目标": "Start@目标" } ] },
              { "Label": "继承干活", "Use": "干活库", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Unconfigured_limit_defaults_to_one_hundred()
    {
        using TestEnvironment environment = TestEnvironment.Create(DefaultLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(100, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "干活").MaxExecutions);
    }

    [Fact]
    public void Configured_limit_takes_effect()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(2, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "干活").MaxExecutions);
    }

    [Fact]
    public void Reference_overrides_and_inherits_library_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "覆盖干活").MaxExecutions);
        Assert.Equal(3, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "继承干活").MaxExecutions);
    }

    /// <summary>执行节点按启动的执行记录计数。</summary>
    private static int Executions(WorkTask task, string label) =>
        task.Snapshot().Executables
            .Single(executable => executable.Executable.Path == task.Context.Graph.PathOf(label))
            .Executions.Count;
}
