using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.TaskExecution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>单节点最高执行次数：路径达上限后不再启动新 run，未成功的停在阻塞且返工不放行。</summary>
public sealed class ExecutionLimitTests
{
    [Fact]
    public void Failed_node_exhausts_limit_and_rejects_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(FailingNodeFlow);
        harness.Executor.FailsWhen = _ => true;

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, task.Runtime.Executable(1).ExecutionCount(null));
        Assert.Contains(blocked.Journal, entry => entry is ErrorEntry error && error.Text.Contains("已达上限"));
        Assert.True(harness.Tasks.Rework(id, null).IsError, "达到上限的节点不能再返工");
    }

    [Fact]
    public void Uncollected_plan_exhausts_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(PlanNodeFlow);
        harness.Executor.SubmitsPlan = false;

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, harness.Registry.Find(id).ThrowIfError().Runtime.Executable(1).ExecutionCount(null));
        Assert.True(harness.Tasks.Rework(id, null).IsError);
    }

    [Fact]
    public void Validation_failure_exhausts_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(ValidationFlow);
        harness.Executor.Output = _ => "产出不达标";

        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, harness.Registry.Find(id).ThrowIfError().Runtime.Executable(1).ExecutionCount(null));
        Assert.True(harness.Tasks.Rework(id, null).IsError);
    }

    /// <summary>环迭代触发的新来源版本被上限拦截：上限低于自然停止点时，修订不再被审查新版本重启。</summary>
    [Fact]
    public void Or_loop_stops_restarting_at_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(OrLoopWithLimitFlow);
        harness.Executor.Output = run => run.Context.NodeIndex == 3 ? "审查通过" : "产出";
        harness.Executor.PortValuesJsonFor = run => run.Context.NodeIndex switch
        {
            3 => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(node => node.Index == 2).Runs);
        Assert.Single(done.Executables.Single(node => node.Index == 4).Runs);
    }

    /// <summary>互驱动环迭代到每节点的执行上限后收敛为完成，验证账本环受 MaxRuns 约束。</summary>
    [Fact]
    public void Or_loop_iterates_to_max_runs()
    {
        using KuroeHarness harness = KuroeHarness.Create(OrLoopWithoutLimitFlow);
        harness.Executor.Output = run => run.Context.NodeIndex == 3 ? "审查通过" : "产出";
        harness.Executor.PortValuesJsonFor = run => run.Context.NodeIndex switch
        {
            3 => """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(5, done.Executables.Single(node => node.Index == 2).Runs.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Index == 3).Runs.Count);
        Assert.Equal(5, done.Executables.Single(node => node.Index == 4).Runs.Count);
    }

    /// <summary>PerItem 展开后各条目独立计数，一条到上限不影响另一条。</summary>
    [Fact]
    public void PerItem_limits_each_item_path_independently()
    {
        using KuroeHarness harness = KuroeHarness.Create(SplitFlowWithLimit);
        harness.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 2 && run.Context.ItemIndex == 1 && run.Context.ExecutionCount == 1;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(2).ExecutionCount(0));
        Assert.Equal(1, task.Runtime.Executable(2).ExecutionCount(1));
        Assert.True(harness.Tasks.Rework(id, null).IsError, "条目路径到上限后不再重跑");
    }

    /// <summary>恒失败的单个节点，满 2 次后停在阻塞。</summary>
    private const string FailingNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者", "MaxRuns": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>规划执行节点不交回拆分，未收口重复到上限后停在阻塞。</summary>
    private const string PlanNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "计划", "Model": "执行者", "Output": "Plan", "MaxRuns": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>校验永远不通过的节点，重复到上限后停在阻塞。</summary>
    private const string ValidationFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者", "Outputs": ["结论"], "MaxRuns": 2,
                "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>修订被计划与审查交替驱动，执行满 1 次即达上限，不再被审查新版本重启。</summary>
    private const string OrLoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "计划", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "修订", "Model": "执行者", "Outputs": ["结论"], "MaxRuns": 1, "From": [{ "Node": "计划@结论", "Or": "初始" }, { "Node": "审查@结论", "Or": "返工" }] },
              { "Name": "审查", "Model": "执行者", "Outputs": ["结论"], "From": ["修订@结论"], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Model": "执行者", "From": ["审查@结论"] }
            ] }
          ] } ]
        }
        """;

    /// <summary>无环外终止的互驱动环：修订被计划与审查交替驱动，全部节点迭代到显式上限后任务收敛为完成。</summary>
    private const string OrLoopWithoutLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "计划", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "修订", "Model": "执行者", "Outputs": ["结论"], "MaxRuns": 5, "From": [{ "Node": "计划@结论", "Or": "初始" }, { "Node": "审查@结论", "Or": "返工" }] },
              { "Name": "审查", "Model": "执行者", "Outputs": ["结论"], "MaxRuns": 5, "From": ["修订@结论"], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Model": "执行者", "MaxRuns": 5, "From": ["审查@结论"] }
            ] }
          ] } ]
        }
        """;

    /// <summary>PerItem 实施节点每条目限一次执行。</summary>
    private const string SplitFlowWithLimit = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "计划", "Model": "执行者", "Output": "Plan" },
              { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "MaxRuns": 1, "From": ["计划@拆分"] }
            ] }
          ] } ]
        }
        """;

    private const string DefaultLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者" }
            ] }
          ] } ]
        }
        """;

    private const string SingleNodeLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "干活", "Model": "执行者", "MaxRuns": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义声明上限，引用侧可覆盖，未覆盖时继承库定义。</summary>
    private const string LibraryLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活库", "Tools": [], "MaxRuns": 3 }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "覆盖干活", "Use": "干活库", "Model": "执行者", "MaxRuns": 5 },
              { "Name": "继承干活", "Use": "干活库", "Model": "执行者" }
            ] }
          ] } ]
        }
        """;

    [Fact]
    public void Unconfigured_limit_defaults_to_one_hundred()
    {
        using KuroeHarness harness = KuroeHarness.Create(DefaultLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(100, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "干活").MaxRuns);
    }

    [Fact]
    public void Configured_limit_takes_effect()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleNodeLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "干活").MaxRuns);
    }

    [Fact]
    public void Reference_overrides_and_inherits_library_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(LibraryLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "覆盖干活").MaxRuns);
        Assert.Equal(3, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "继承干活").MaxRuns);
    }
}
