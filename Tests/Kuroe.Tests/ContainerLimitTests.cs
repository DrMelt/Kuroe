using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Execution.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器统一执行次数上限：组内执行节点未配置时生效，嵌套与引用按就近覆盖解析。</summary>
public sealed class ContainerLimitTests
{
    /// <summary>内联容器配置 MaxRuns，组内未配置的执行节点统一生效。</summary>
    [Fact]
    public void Inline_container_limit_applies_to_unconfigured_members()
    {
        using TestEnvironment environment = TestEnvironment.Create(InlineContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.All(task.Graph.ExecutableNodes, node => Assert.Equal(2, node.MaxRuns));
    }

    /// <summary>执行节点的显式配置优先于容器统一上限。</summary>
    [Fact]
    public void Explicit_member_limit_overrides_container()
    {
        using TestEnvironment environment = TestEnvironment.Create(ExplicitMemberLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "重点").MaxRuns);
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "普通").MaxRuns);
    }

    /// <summary>嵌套容器的统一上限覆盖外层容器，同层其余成员保留外层的值。</summary>
    [Fact]
    public void Nested_container_limit_overrides_outer()
    {
        using TestEnvironment environment = TestEnvironment.Create(NestedContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "内层干活").MaxRuns);
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "外层干活").MaxRuns);
    }

    /// <summary>引用容器时装配层可覆盖库定义统一上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_container_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "较重.干活").MaxRuns);
        Assert.Equal(3, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "较轻.干活").MaxRuns);
    }

    /// <summary>容器统一上限到点后，组内失败的执行节点停驻且返工不放行。</summary>
    [Fact]
    public void Container_limit_blocks_failed_member_at_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleRunContainerFlow);
        environment.Executor.FailsWhen = _ => true;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(2).ExecutionCount(null));
        Assert.True(environment.Tasks.Rework(id, null).IsError, "达到上限的节点不能再返工");
    }

    private const string InlineContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxRuns": 2, "Nodes": [
              { "Name": "计划", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "计划@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ExplicitMemberLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxRuns": 2, "Nodes": [
              { "Name": "重点", "Kind": "Llm", "Model": "执行者", "MaxRuns": 5, "From": [ { "目标": "Start@目标" } ] },
              { "Name": "普通", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;

    private const string NestedContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxRuns": 2, "Nodes": [
              { "Name": "外层干活", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "内部", "Kind": "Container", "MaxRuns": 5, "Nodes": [
                { "Name": "内层干活", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "外层干活@结论" } ] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库容器定义声明统一上限，引用处可覆盖或继承。</summary>
    private const string ReferenceContainerLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活组", "Kind": "Container", "Models": ["执行者"], "MaxRuns": 3, "Inputs": [{ "Name": "入口" }], "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "入口": "@入口" } ] }
            ] }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "较重", "Use": "干活组", "MaxRuns": 5, "In": { "入口": "Start@目标" }, "Models": { "执行者": "执行者" } },
              { "Name": "较轻", "Use": "干活组", "In": { "入口": "Start@目标" }, "Models": { "执行者": "执行者" } }
            ] }
          ] } ]
        }
        """;

    private const string SingleRunContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "MaxRuns": 1, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
            ] }
          ] } ]
        }
        """;
}
