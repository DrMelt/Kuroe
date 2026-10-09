using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests;

/// <summary>库容器输出端口：装配层下游按 容器@端口 消费容器内成员的命名段。</summary>
public sealed class ContainerOutPortTests
{
    [Fact]
    public void Container_out_port_consumes_bound_member_named_segment()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ContainerOutFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 容器内实施成员放行命名段产出后，下游按容器输出端口取到对应命名段，出处指向容器
        RunSnapshot retrospective = done.Executables
            .Single(entry => entry.Executable.Name.Value == "复盘")
            .Runs.Single();
        Assert.Contains(retrospective.Context.Seed,
            message => message.Text.Contains("节点「交付」的端口「结论」产出")
                && message.Text.Contains("结论产出"));
        Assert.DoesNotContain(retrospective.Context.Seed, message => message.Text.Contains("理由产出"));
    }

    [Fact]
    public void Container_out_port_without_submitted_values_blocks()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ContainerOutFlow);
        harness.Executor.SubmitsPorts = false;

        TaskId id = harness.Submit("目标");
        TaskSnapshot blocked = harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.ExecutableStates, state => state.State == NodeState.Blocked);
    }
}
