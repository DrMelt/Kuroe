using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>门控推进：阻塞、重新执行与 From 汇合。</summary>
public sealed class GateAdvanceTests
{
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
        environment.Runner.PortValuesJsonFor = execution =>
        {
            if (execution.Input.Node.Label.Value != "修订")
            {
                return null;
            }

            return execution.Input.ExecutionCount == 1
                ? "{}"
                : """{"结论": "修订产出已通过"}""";
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Empty(blocked.View.Steps[2].Executions);

        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.View.Steps[2].Executions);
    }

    /// <summary>汇合的 From 来源甲依赖乙，乙先齐备时 From 未齐备不得启动；乙、甲都齐备后才启动一次。</summary>
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
        Assert.Single(done.View.Steps[2].Executions);
    }

    [Fact]
    public async Task Container_source_waits_and_resumes_with_revision()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerGateFlow);
        environment.Runner.Reply = execution => execution.Input.Node.Label.Value switch
        {
            "甲" => execution.Input.ExecutionCount == 1 ? "甲产出不达标" : "甲产出已通过",
            _ => "闸门产出",
        };
        environment.Runner.PortValuesJsonFor = execution =>
        {
            if (execution.Input.Node.Label.Value != "甲")
            {
                return null;
            }

            return execution.Input.ExecutionCount == 1
                ? "{}"
                : """{"结论": "甲产出已通过"}""";
        };

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = await environment.SettleAsync(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(FlowNodeState.Blocked, Assert.Single(blocked.View.Steps, step => step.Label == "甲").State);
        Assert.Empty(blocked.View.Steps.Single(step => step.Label == "闸门").Executions);

        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot done = await environment.SettleAsync(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.View.Steps.Single(step => step.Label == "甲").Executions.Count);
        Assert.Single(done.View.Steps.Single(step => step.Label == "闸门").Executions);
    }

    /// <summary>汇合的 From 来源甲依赖乙，乙先齐备时 From 未齐备不得启动；乙、甲都齐备后才启动一次。</summary>
    private const string FromGateFlow = """
        { "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
          { "Name": "甲", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "乙@结论" } ] },
          { "Name": "乙", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
          { "Name": "汇合", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "甲": "甲@结论", "乙": "乙@结论" } ] }
        ] } } ] }
        """;

    private const string FreshGateFlow = """
        { "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
          { "Name": "计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
          { "Name": "修订", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "计划@结论" } ] },
          { "Name": "汇合", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "修订@结论" } ] }
        ] } } ] }
        """;

    /// <summary>容器作组来源：成员阻塞时容器不可用，成员恢复齐备后按容器代数驱动启动。</summary>
    private const string ContainerGateFlow = """
        { "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
          { "Name": "汇聚", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "甲@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ], "Nodes": [
            { "Name": "甲", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
          ] },
          { "Name": "闸门", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "汇聚@结论" } ] }
        ] } } ] }
        """;
}
