using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Nodes;

/// <summary>库容器输出端口：装配层下游按 容器@端口 消费容器内成员的命名段。</summary>
public sealed class ContainerOutputsTests
{
    [Fact]
    public void Container_out_port_consumes_bound_member_named_segment()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ContainerOutputsFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        // 容器内实施成员放行命名段产出后，下游按容器输出端口取到对应命名段，出处指向容器
        ExecutionSnapshot retrospective = done.Executables
            .Single(entry => entry.Executable.Label.Value == "复盘")
            .Executions.Single();
        Assert.Contains(retrospective.Input.Inputs, value =>
            value.Origin is ContainerPortSource source
                && source.Port == new PortName("结论")
                && value.Text.Contains("结论产出"));
        Assert.DoesNotContain(retrospective.Input.Inputs, value => value.Text.Contains("理由产出"));
    }
}
