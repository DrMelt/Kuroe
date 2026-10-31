using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Integration.Tests.Workflows.Tasks;

/// <summary>宿主开始输入接入任务提交：流程使用模型宿主输入但没有选择模型时，提交阶段报模型未选择。</summary>
public sealed class KuroeTaskStartAcceptanceTests
{
    [Fact]
    public void Submit_without_a_selected_model_reports_it()
    {
        using TestEnvironment environment = TestEnvironment.Create(seedCatalog: false);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "目标"),
            new FlowName("默认"),
            null);

        Assert.True(submitted.IsError);
        Assert.Equal("当前未选择模型。", submitted.FirstError.Description);
    }
}
