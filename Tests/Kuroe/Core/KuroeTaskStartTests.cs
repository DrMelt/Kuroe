using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>宿主开始输入：未知名称拒绝，模型输入取当前选择，未选择时拒绝。</summary>
public sealed class KuroeTaskStartTests
{
    private static readonly PortName Model = new("MODEL_NAME");

    [Fact]
    public void Model_input_takes_the_current_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<IPortTexts> values = Start(environment).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = StartSources.Model },
            new FlowName("默认"),
            "标题");

        Assert.False(values.IsError, string.Join("；", values.ErrorsOrEmptyList.Select(error => error.Description)));
        Assert.Equal("fake", values.Value.Text(Model));
    }

    [Fact]
    public void Model_input_without_a_selection_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Models.Unselect().ThrowIfError();

        ErrorOr<IPortTexts> values = Start(environment).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = StartSources.Model },
            new FlowName("默认"),
            "标题");

        Assert.True(values.IsError);
        Assert.Equal("当前未选择模型。", values.FirstError.Description);
    }

    [Fact]
    public void Unknown_host_input_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<IPortTexts> values = Start(environment).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = "NOPE" },
            new FlowName("默认"),
            "标题");

        Assert.True(values.IsError);
        Assert.Contains("未知的宿主开始输入 NOPE", values.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>流程使用模型宿主输入但没有选择模型时，提交阶段报模型未选择。</summary>
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

    private static KuroeTaskStart Start(TestEnvironment environment) =>
        new(environment.Root, null, environment.Models);
}