using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器统一执行次数上限：组内执行节点未配置时生效，嵌套与引用按就近覆盖解析。</summary>
public sealed class ContainerLimitTests
{
    /// <summary>内联容器配置 MaxExecutions，组内未配置的执行节点统一生效。</summary>
    [Fact]
    public void Inline_container_limit_applies_to_unconfigured_members()
    {
        using TestEnvironment environment = TestEnvironment.Create(InlineContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.All(task.Context.Graph.ExecutableNodes, node => Assert.Equal(2, node.MaxExecutions));
    }

    /// <summary>执行节点的显式配置优先于容器统一上限。</summary>
    [Fact]
    public void Explicit_member_limit_overrides_container()
    {
        using TestEnvironment environment = TestEnvironment.Create(ExplicitMemberLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "重点").MaxExecutions);
        Assert.Equal(2, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "普通").MaxExecutions);
    }

    /// <summary>嵌套容器的统一上限覆盖外层容器，同层其余成员保留外层的值。</summary>
    [Fact]
    public void Nested_container_limit_overrides_outer()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "内层干活").MaxExecutions);
        Assert.Equal(2, task.Context.Graph.ExecutableNodes.Single(node => node.Label.Value == "外层干活").MaxExecutions);
    }

    /// <summary>引用容器时装配层可覆盖库定义统一上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_container_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Context.Graph.ExecutableNodes.Single(node => node.Path.Value == "整体/较重/干活").MaxExecutions);
        Assert.Equal(3, task.Context.Graph.ExecutableNodes.Single(node => node.Path.Value == "整体/较轻/干活").MaxExecutions);
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

    private const string InlineContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxExecutions": 2, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "计划@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ExplicitMemberLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxExecutions": 2, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "重点", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 5, "From": [ { "目标": "@目标" } ] },
              { "Name": "普通", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    private const string NestedContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxExecutions": 2, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "外层干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "内部", "Kind": "Container", "MaxExecutions": 5, "Inputs": [{ "Name": "结论" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "结论": "外层干活@结论", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                { "Name": "内层干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "@结论" } ] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库容器定义声明统一上限，引用处可覆盖或继承。</summary>
    private const string ReferenceContainerLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活组", "Kind": "Container", "MaxExecutions": 3, "Inputs": [{ "Name": "入口" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "入口": "@入口" } ] }
            ] }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "较重", "Use": "干活组", "MaxExecutions": 5, "In": { "入口": "@目标", "MODEL_NAME": "@MODEL_NAME" } },
              { "Label": "较轻", "Use": "干活组", "In": { "入口": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ] } ]
        }
        """;

    private const string SingleExecutionContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxExecutions": 1, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
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
