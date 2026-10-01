using ErrorOr;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输出校验推进：通过放行、不通过阻塞、返工恢复与配置校验。</summary>
public sealed class ValidationAdvanceTests
{
    [Fact]
    public void Validate_pass_releases_downstream()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewReviseFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            2 => "修订产出已通过",
            _ => "交付产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Runs);
        Assert.Single(done.Executables[1].Runs);
        Assert.Single(done.Executables[2].Runs);
    }

    [Fact]
    public void Validate_failure_blocks_node_without_releasing()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewReviseFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            2 => "修订产出不够好",
            _ => "交付产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 2).State);
        Assert.Empty(blocked.Executables[2].Runs);
    }

    [Fact]
    public void Validate_rework_resumes_and_passes()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewReviseFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            2 => run.Context.ExecutionCount == 1 ? "修订产出不够好" : "修订产出已通过",
            _ => "交付产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables[1].Runs.Count);
        Assert.Single(done.Executables[2].Runs);
    }

    [Theory]
    [InlineData("NonEmpty", null, "有内容", true)]
    [InlineData("NonEmpty", null, "", false)]
    [InlineData("TextContains", "通过", "修订产出已通过", true)]
    [InlineData("TextContains", "通过", "修订产出不达标", false)]
    [InlineData("TextNot", "坏词", "修订产出干净", true)]
    [InlineData("TextNot", "坏词", "修订产出含坏词", false)]
    [InlineData("TextNot", "坏词", "", false)]
    [InlineData("TextEquals", "完全一致", "完全一致", true)]
    [InlineData("TextEquals", "完全一致", "略有差异", false)]
    [InlineData("Pattern", "NEVER", "这样的结果 NEVER 出现", true)]
    [InlineData("Pattern", "NEVER", "偶尔会", false)]
    public void Validation_predicate_decides_release(string predicate, string? argument, string output, bool expectPass)
    {
        string argumentJson = argument is null ? "" : $", \"Argument\": \"{argument}\"";
        string flow = $$"""
            { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
              { "Name": "整体", "Nodes": [
                { "Name": "计划", "Model": "执行者" },
                { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "{{predicate}}"{{argumentJson}} } }
              ] }
            ] } ] }
            """;

        using KuroeHarness harness = KuroeHarness.Create(flow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            _ => output,
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot snapshot = harness.Settle(id);

        if (expectPass)
        {
            Assert.Equal(TaskState.Done, snapshot.State);
            Assert.Single(snapshot.Executables[1].Runs);
        }
        else
        {
            Assert.Equal(TaskState.Blocked, snapshot.State);
            Assert.Equal(NodeState.Blocked, Assert.Single(snapshot.ExecutableStates, state => state.Index == 2).State);
        }
    }

    [Fact]
    public void AnyOf_node_stays_gated_until_source_releases_new_version()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfFreshGateFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            2 => run.Context.ExecutionCount == 1 ? "修订产出不够好" : "修订产出已通过",
            _ => "汇合产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Empty(blocked.Executables[2].Runs);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Runs);
    }

    [Theory]
    [InlineData(BadPredicate, "Validate.Predicate 应为")]
    [InlineData(BadPattern, "正则不合法")]
    [InlineData(ValidateOnPerItem, "不能按条目展开")]
    [InlineData(AnyOfMissingNode, "不在流程里")]
    [InlineData(AnyOfEmptyGroup, "组不能为空")]
    [InlineData(AnyOfDuplicateGroup, "各组必须整体不同")]
    [InlineData(NumericPredicate, "Validate.Predicate 应为")]
    [InlineData(NonEmptyWithArgument, "不接受参数")]
    [InlineData(TextAssertMissingArgument, "需要指定文本参数")]
    public void Invalid_validation_flow_fails_setup(string flowsJson, string expected)
    {
        ErrorOr<KuroeHarness> harness = KuroeHarness.TryCreate(flowsJson);

        Assert.True(harness.IsError);
        Assert.Contains(harness.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    [Fact]
    public void AnyOf_repeats_within_and_across_groups_are_allowed()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfRepeatsFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "甲产出",
            2 => "乙产出",
            _ => "汇合产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.NotEmpty(done.Executables[2].Runs);
    }

    [Fact]
    public void AnyOf_reordered_groups_are_distinct()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfReorderedFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "甲产出",
            2 => "乙产出",
            _ => "汇合产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Runs);
    }

    [Fact]
    public void AnyOf_from_must_be_ready_before_any_group_starts()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfFromGateFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "甲产出",
            2 => "乙产出",
            _ => "汇合产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Runs);
    }

    [Fact]
    public void AnyOf_container_source_waits_and_resumes_with_generation()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerAnyOfGateFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            2 => run.Context.ExecutionCount == 1 ? "甲产出不达标" : "甲产出已通过",
            _ => "闸门产出",
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 2).State);
        Assert.Empty(blocked.Executables.Single(snapshot => snapshot.Index == 3).Runs);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(snapshot => snapshot.Index == 2).Runs.Count);
        Assert.Single(done.Executables.Single(snapshot => snapshot.Index == 3).Runs);
    }

    [Fact]
    public void AnyOf_loop_restarts_writer_on_reviewer_new_version()
    {
        using KuroeHarness harness = KuroeHarness.Create(AnyOfLoopFlow);
        harness.Executor.Output = run => run.Context.NodeIndex switch
        {
            1 => "计划产出",
            2 => "修订产出",
            3 => run.Context.ExecutionCount == 1 ? "审查未达标准" : "审查通过",
            _ => "交付产出",
        };

        TaskId id = harness.Submit("补齐 README");

        // 首次：计划驱动修订初稿，审查判定未达标准停驻
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 3).State);
        Assert.Empty(blocked.Executables[3].Runs);

        // 返工：审查通过并放行后，修订被审查的新版本再次驱动，交付随终稿放行
        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Runs);
        Assert.Equal(2, done.Executables[1].Runs.Count);
        Assert.Equal(2, done.Executables[2].Runs.Count);
        Assert.Single(done.Executables[3].Runs);
    }

    /// <summary>修订从计划取输入，交付是修订的环外下游；校验决定修订产出能否放行。</summary>
    private const string ReviewReviseFlow = """"
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "计划", "Model": "执行者" },
                    { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
                    { "Name": "交付", "Model": "执行者", "From": ["修订"] }
                  ]
                }
              ]
            }
          ]
        }
        """";

    private const string BadPredicate = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "Sprout" } }
          ] }
        ] } ] }
        """";

    private const string NumericPredicate = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "2" } }
          ] }
        ] } ] }
        """";

    private const string NonEmptyWithArgument = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "NonEmpty", "Argument": "多余" } }
          ] }
        ] } ] }
        """";

    private const string TextAssertMissingArgument = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "TextContains" } }
          ] }
        ] } ] }
        """";

    private const string BadPattern = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "Pattern", "Argument": "[" } }
          ] }
        ] } ] }
        """";

    private const string ValidateOnPerItem = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者", "Output": "Plan" },
            { "Name": "实施", "Model": "执行者", "Mode": "PerItem", "From": ["计划"], "Validate": { "Predicate": "NonEmpty" } }
          ] }
        ] } ] }
        """";

    private const string AnyOfMissingNode = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "AnyOf": [["不存在的节点"]] }
          ] }
        ] } ] }
        """";

    private const string AnyOfEmptyGroup = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "AnyOf": [[]] }
          ] }
        ] } ] }
        """;

    private const string AnyOfDuplicateGroup = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "AnyOf": [["计划", "修订"], ["计划", "修订"]] }
          ] }
        ] } ] }
        """;

    /// <summary>汇合的 From 来源甲依赖乙，乙先齐备时 From 未齐备不得启动；乙、甲都齐备后才启动一次。</summary>
    private const string AnyOfFromGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "甲", "Model": "执行者", "From": ["乙"] },
            { "Name": "乙", "Model": "执行者" },
            { "Name": "汇合", "Model": "执行者", "From": ["甲"], "AnyOf": [["乙"]] }
          ] }
        ] } ] }
        """;

    private const string AnyOfFreshGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "计划", "Model": "执行者" },
            { "Name": "修订", "Model": "执行者", "From": ["计划"], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
            { "Name": "汇合", "Model": "执行者", "AnyOf": [["修订"]] }
          ] }
        ] } ] }
        """;

    /// <summary>组内与组间允许重复引用来源：甲独占一组并组内重复，甲与乙组成的另一组跨组共享甲。</summary>
    private const string AnyOfRepeatsFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "甲", "Model": "执行者" },
            { "Name": "乙", "Model": "执行者" },
            { "Name": "汇合", "Model": "执行者", "AnyOf": [["甲", "甲"], ["甲", "乙"]] }
          ] }
        ] } ] }
        """;

    /// <summary>成员顺序不同的组是不同组，可同时声明。</summary>
    private const string AnyOfReorderedFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "甲", "Model": "执行者" },
            { "Name": "乙", "Model": "执行者" },
            { "Name": "汇合", "Model": "执行者", "AnyOf": [["甲", "乙"], ["乙", "甲"]] }
          ] }
        ] } ] }
        """;

    /// <summary>容器作 AnyOf 来源：成员阻塞时容器不可用，成员恢复齐备后按容器代数驱动启动。</summary>
    private const string ContainerAnyOfGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "汇聚", "Nodes": [
              { "Name": "甲", "Model": "执行者", "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] },
            { "Name": "闸门", "Model": "执行者", "AnyOf": [["汇聚"]] }
          ] }
        ] } ] }
        """;
    /// <summary>允许的环：修订被计划驱动，审查按修订产出复核，返工后审查放行又驱动修订重写，交付取审查放行后的定稿。</summary>
    private const string AnyOfLoopFlow = """"
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "计划", "Model": "执行者" },
              { "Name": "修订", "Model": "执行者", "AnyOf": [["计划"], ["审查"]] },
              { "Name": "审查", "Model": "执行者", "From": ["修订"], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Model": "执行者", "From": ["审查"] }
            ] }
          ] } ]
        }
        """";
}
