using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>单节点最高执行次数：路径达上限后不再启动新 run，未成功的停在阻塞且返工不放行。</summary>
public sealed class ExecutionLimitTests
{
    [Fact]
    public void Failed_node_exhausts_limit_and_rejects_rework()
    {
        using TestEnvironment environment = TestEnvironment.Create(FailingNodeFlow);
        environment.Executor.FailsWhen = _ => true;

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, task.Runtime.Executable(task.Graph.PathOf("干活")).ExecutionCount(null));
        Assert.Contains(blocked.Journal, entry => entry is ErrorEntry error && error.Text.Contains("已达上限"));
        Assert.True(environment.Tasks.Rework(id, null).IsError, "达到上限的节点不能再返工");
    }

    [Fact]
    public void Uncollected_plan_exhausts_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(PlanNodeFlow);
        environment.Executor.SubmitsPlan = false;

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, task.Runtime.Executable(task.Graph.PathOf("计划")).ExecutionCount(null));
        Assert.True(environment.Tasks.Rework(id, null).IsError);
    }

    [Fact]
    public void Validation_failure_exhausts_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ValidationFlow);
        environment.Executor.Output = _ => "产出不达标";

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, task.Runtime.Executable(task.Graph.PathOf("干活")).ExecutionCount(null));
        Assert.True(environment.Tasks.Rework(id, null).IsError);
    }

    /// <summary>环迭代触发的新来源版本被上限拦截：上限低于自然停止点时，修订不再被审查新版本重启。</summary>
    [Fact]
    public void Loop_stops_restarting_at_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopWithLimitFlow);
        environment.Executor.Output = run => run.Context.Node.Name.Value == "审查" ? "审查通过" : "产出";
        environment.Executor.PortValuesJsonFor = run => run.Context.Node.Name.Value switch
        {
            "审查" => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(node => node.Executable.Name == new NodeName("修订")).Runs);
        Assert.Single(done.Executables.Single(node => node.Executable.Name == new NodeName("交付")).Runs);
    }

    /// <summary>互驱动环迭代到每节点的执行上限后收敛为完成，验证账本环受 MaxRuns 约束。</summary>
    [Fact]
    public void Loop_iterates_to_max_runs()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopWithoutLimitFlow);
        environment.Executor.Output = run => run.Context.Node.Name.Value == "审查" ? "审查通过" : "产出";
        environment.Executor.PortValuesJsonFor = run => run.Context.Node.Name.Value switch
        {
            "审查" => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Name == new NodeName("修订")).Runs.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Name == new NodeName("审查")).Runs.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Executable.Name == new NodeName("交付")).Runs.Count);
    }

    /// <summary>PerItem 展开后各条目独立计数，一条到上限不影响另一条。</summary>
    [Fact]
    public void PerItem_limits_each_item_path_independently()
    {
        using TestEnvironment environment = TestEnvironment.Create(SplitFlowWithLimit);
        environment.Executor.FailsWhen = run =>
            run.Context.Node.Name.Value == "实施" && run.Context.ItemIndex == 1 && run.Context.ExecutionCount == 1;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        NodePath implement = task.Graph.PathOf("实施");
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(implement).ExecutionCount(0));
        Assert.Equal(1, task.Runtime.Executable(implement).ExecutionCount(1));
        Assert.True(environment.Tasks.Rework(id, null).IsError, "条目路径到上限后不再重跑");
    }

    /// <summary>恒失败的单个节点，满 2 次后停在阻塞。</summary>
    private const string FailingNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "MaxRuns": 2, "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>声明 WorkItems 端口的执行节点不交回拆分，未收口重复到上限后停在阻塞。</summary>
    private const string PlanNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "MaxRuns": 2, "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>校验永远不通过的节点，重复到上限后停在阻塞。</summary>
    private const string ValidationFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxRuns": 2, "From": [ { "目标": "Start@目标" } ],
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
              { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxRuns": 1, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
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
              { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxRuns": 5, "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxRuns": 5, "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Model": "执行者", "MaxRuns": 5, "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>PerItem 实施节点每条目限一次执行。</summary>
    private const string SplitFlowWithLimit = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem", "MaxRuns": 1, "From": [ { "拆分": "计划@拆分" } ] }
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

    private const string SingleNodeLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "MaxRuns": 2, "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义声明上限，引用侧可覆盖，未覆盖时继承库定义。</summary>
    private const string LibraryLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活库", "Kind": "Llm", "Tools": [], "MaxRuns": 3 }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "覆盖干活", "Use": "干活库", "Model": "执行者", "MaxRuns": 5, "From": [ { "目标": "Start@目标" } ] },
              { "Name": "继承干活", "Use": "干活库", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
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
        Assert.Equal(100, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "干活").MaxRuns);
    }

    [Fact]
    public void Configured_limit_takes_effect()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleNodeLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "干活").MaxRuns);
    }

    [Fact]
    public void Reference_overrides_and_inherits_library_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "覆盖干活").MaxRuns);
        Assert.Equal(3, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "继承干活").MaxRuns);
    }
}
