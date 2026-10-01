using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>容器统一执行次数上限：组内执行节点未配置时生效，嵌套与引用按就近覆盖解析。</summary>
public sealed class ContainerLimitTests
{
    /// <summary>内联容器配置 MaxRuns，组内未配置的执行节点统一生效。</summary>
    [Fact]
    public void Inline_container_limit_applies_to_unconfigured_members()
    {
        using KuroeHarness harness = KuroeHarness.Create(InlineContainerLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.All(task.Graph.ExecutableNodes, node => Assert.Equal(2, node.MaxRuns));
    }

    /// <summary>执行节点的显式配置优先于容器统一上限。</summary>
    [Fact]
    public void Explicit_member_limit_overrides_container()
    {
        using KuroeHarness harness = KuroeHarness.Create(ExplicitMemberLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "重点").MaxRuns);
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "普通").MaxRuns);
    }

    /// <summary>嵌套容器的统一上限覆盖外层容器，同层其余成员保留外层的值。</summary>
    [Fact]
    public void Nested_container_limit_overrides_outer()
    {
        using KuroeHarness harness = KuroeHarness.Create(NestedContainerLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "内层干活").MaxRuns);
        Assert.Equal(2, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "外层干活").MaxRuns);
    }

    /// <summary>引用容器时装配层可覆盖库定义统一上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_container_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReferenceContainerLimitFlow);
        TaskId id = harness.Submit("补齐 README");
        harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "较重.干活").MaxRuns);
        Assert.Equal(3, task.Graph.ExecutableNodes.Single(node => node.Name.Value == "较轻.干活").MaxRuns);
    }

    /// <summary>容器统一上限到点后，组内失败的执行节点停驻且返工不放行。</summary>
    [Fact]
    public void Container_limit_blocks_failed_member_at_limit()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleRunContainerFlow);
        harness.Executor.FailsWhen = _ => true;

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);

        WorkTask task = harness.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, task.Runtime.Executable(1).ExecutionCount(null));
        Assert.True(harness.Tasks.Rework(id, null).IsError, "达到上限的节点不能再返工");
    }

    private const string InlineContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "MaxRuns": 2, "Nodes": [
              { "Name": "计划", "Model": "执行者", "Output": "Plan" },
              { "Name": "干活", "Model": "执行者", "From": ["计划"] }
            ] }
          ] } ]
        }
        """;

    private const string ExplicitMemberLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "MaxRuns": 2, "Nodes": [
              { "Name": "重点", "Model": "执行者", "MaxRuns": 5 },
              { "Name": "普通", "Model": "执行者" }
            ] }
          ] } ]
        }
        """;

    private const string NestedContainerLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "MaxRuns": 2, "Nodes": [
              { "Name": "外层干活", "Model": "执行者" },
              { "Name": "内部", "MaxRuns": 5, "Nodes": [
                { "Name": "内层干活", "Model": "执行者", "From": ["外层干活"] }
              ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>库容器定义声明统一上限，引用处可覆盖或继承。</summary>
    private const string ReferenceContainerLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活组", "MaxRuns": 3, "Nodes": [
              { "Name": "干活", "Model": "执行者" }
            ] }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "较重", "Use": "干活组", "MaxRuns": 5, "Models": { "执行者": "执行者" } },
              { "Name": "较轻", "Use": "干活组", "Models": { "执行者": "执行者" } }
            ] }
          ] } ]
        }
        """;

    private const string SingleRunContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "MaxRuns": 1, "Nodes": [
              { "Name": "干活", "Model": "执行者" }
            ] }
          ] } ]
        }
        """;
}
