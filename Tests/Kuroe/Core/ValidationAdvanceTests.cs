using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输出校验推进：通过放行、不通过阻塞、返工恢复与配置校验。</summary>
public sealed class ValidationAdvanceTests
{
    [Fact]
    public void Validate_pass_releases_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewReviseFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            "修订" => "修订产出已通过",
            _ => "交付产出",
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "修订" => """{"结论": "修订产出已通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Executions);
        Assert.Single(done.Executables[1].Executions);
        Assert.Single(done.Executables[2].Executions);
    }

    [Fact]
    public void Validate_failure_blocks_node_without_releasing()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewReviseFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            "修订" => "修订产出不够好",
            _ => "交付产出",
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Node.Label == new NodeLabel("修订")).State);
        Assert.Empty(blocked.Executables[2].Executions);
    }

    [Fact]
    public void Validate_rework_resumes_and_passes()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewReviseFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            "修订" => execution.Input.ExecutionCount == 1 ? "修订产出不够好" : "修订产出已通过",
            _ => "交付产出",
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "修订" => execution.Input.ExecutionCount == 1
                ? """{"结论": "修订产出不够好"}"""
                : """{"结论": "修订产出已通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables[1].Executions.Count);
        Assert.Single(done.Executables[2].Executions);
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
            { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
              { "Name": "整体", "Kind": "Container", "Nodes": [
                { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "{{predicate}}"{{argumentJson}} } }
              ] }
            ] } ] }
            """;

        using TestEnvironment environment = TestEnvironment.Create(flow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            _ => output,
        };
        environment.Runner.PortValuesJsonFor = execution =>
            execution.Input.Node.Label.Value == "修订" ? $$"""{"结论": "{{output}}"}""" : null;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot snapshot = environment.Settle(id);

        if (expectPass)
        {
            Assert.Equal(TaskState.Done, snapshot.State);
            Assert.Single(snapshot.Executables[1].Executions);
        }
        else
        {
            Assert.Equal(TaskState.Blocked, snapshot.State);
            Assert.Equal(NodeState.Blocked, Assert.Single(snapshot.ExecutableStates, state => state.Node.Label == new NodeLabel("修订")).State);
        }
    }

    [Fact]
    public void Group_stays_gated_until_source_releases_new_version()
    {
        using TestEnvironment environment = TestEnvironment.Create(FreshGateFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            "修订" => execution.Input.ExecutionCount == 1 ? "修订产出不够好" : "修订产出已通过",
            _ => "汇合产出",
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "修订" => execution.Input.ExecutionCount == 1
                ? """{"结论": "修订产出不够好"}"""
                : """{"结论": "修订产出已通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Empty(blocked.Executables[2].Executions);

        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Executions);
    }

    [Theory]
    [InlineData(BadPredicate, "Validate.Predicate 应为")]
    [InlineData(BadPattern, "正则不合法")]
    [InlineData(OrMissingNode, "不在流程里")]
    [InlineData(NumericPredicate, "Validate.Predicate 应为")]
    [InlineData(NonEmptyWithArgument, "不接受参数")]
    [InlineData(TextAssertMissingArgument, "需要指定文本参数")]
    public void Invalid_validation_flow_fails_setup(string flowsJson, string expected)
    {
        ErrorOr<TestEnvironment> environment = TestEnvironment.TryCreate(flowsJson);

        Assert.True(environment.IsError);
        Assert.Contains(environment.ErrorsOrEmptyList, error => error.Description.Contains(expected));
    }

    [Fact]
    public void Group_must_be_ready_before_start()
    {
        using TestEnvironment environment = TestEnvironment.Create(FromGateFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "甲" => "甲产出",
            "乙" => "乙产出",
            _ => "汇合产出",
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[2].Executions);
    }

    [Fact]
    public void Container_source_waits_and_resumes_with_revision()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerGateFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "甲" => execution.Input.ExecutionCount == 1 ? "甲产出不达标" : "甲产出已通过",
            _ => "闸门产出",
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "甲" => execution.Input.ExecutionCount == 1
                ? """{"结论": "甲产出不达标"}"""
                : """{"结论": "甲产出已通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Node.Label == new NodeLabel("甲")).State);
        Assert.Empty(blocked.Executables.Single(snapshot => snapshot.Executable.Label == new NodeLabel("闸门")).Executions);

        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(snapshot => snapshot.Executable.Label == new NodeLabel("甲")).Executions.Count);
        Assert.Single(done.Executables.Single(snapshot => snapshot.Executable.Label == new NodeLabel("闸门")).Executions);
    }

    [Fact]
    public void Loop_restarts_writer_on_reviewer_new_version()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "计划" => "计划产出",
            "修订" => "修订产出",
            "审查" => execution.Input.ExecutionCount == 1 ? "审查未达标准" : "审查通过",
            _ => "交付产出",
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "审查" => execution.Input.ExecutionCount == 1
                ? """{"结论": "审查未达标准"}"""
                : """{"结论": "审查通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("补齐 README");

        // 首次：计划驱动修订初稿，审查判定未达标准停驻
        TaskSnapshot blocked = environment.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Node.Label == new NodeLabel("审查")).State);
        Assert.Empty(blocked.Executables[3].Executions);

        // 返工：审查通过并放行后，修订被审查的新版本再次驱动，交付随终稿放行
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables[0].Executions);
        Assert.Equal(2, done.Executables[1].Executions.Count);
        Assert.Equal(2, done.Executables[2].Executions.Count);
        Assert.Single(done.Executables[3].Executions);
    }

    /// <summary>修订从计划取输入，交付是修订的环外下游；校验决定修订产出能否放行。</summary>
    private const string ReviewReviseFlow = """"
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
                    { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
                    { "Name": "交付", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "修订@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """";

    private const string BadPredicate = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "Sprout" } }
          ] }
        ] } ] }
        """";

    private const string NumericPredicate = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "2" } }
          ] }
        ] } ] }
        """";

    private const string NonEmptyWithArgument = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "NonEmpty", "Argument": "多余" } }
          ] }
        ] } ] }
        """";

    private const string TextAssertMissingArgument = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "TextContains" } }
          ] }
        ] } ] }
        """";

    private const string BadPattern = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "Pattern", "Argument": "[" } }
          ] }
        ] } ] }
        """";

    private const string OrMissingNode = """"
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "不存在的节点@结论" } ] }
          ] }
        ] } ] }
        """";

    /// <summary>汇合的 From 来源甲依赖乙，乙先齐备时 From 未齐备不得启动；乙、甲都齐备后才启动一次。</summary>
    private const string FromGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "甲", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "乙@结论" } ] },
            { "Name": "乙", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "汇合", "Kind": "Llm", "Model": "执行者", "From": [ { "甲": "甲@结论", "乙": "乙@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string FreshGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
            { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
            { "Name": "汇合", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "修订@结论" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>容器作组来源：成员阻塞时容器不可用，成员恢复齐备后按容器代数驱动启动。</summary>
    private const string ContainerGateFlow = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "汇聚", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "甲@结论" }], "Nodes": [
              { "Name": "甲", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Validate": { "Predicate": "TextContains", "Argument": "通过" }, "From": [ { "目标": "Start@目标" } ] }
            ] },
            { "Name": "闸门", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "汇聚@结论" } ] }
          ] }
        ] } ] }
        """;
    /// <summary>允许的环：修订被计划驱动，审查按修订产出复核，返工后审查放行又驱动修订重写，交付取审查放行后的定稿。
    /// 审查以 MaxExecutions 封顶，避免放行后修订互驱无限迭代。</summary>
    private const string LoopFlow = """"
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "修订", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" }, { "结论": "审查@结论" } ] },
              { "Name": "审查", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "MaxExecutions": 2, "From": [ { "结论": "修订@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } },
              { "Name": "交付", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "审查@结论" } ] }
            ] }
          ] } ]
        }
        """";
}
