using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Tests.Support;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests.Workflows.Tasks;

/// <summary>宿主开始输入：未知名称拒绝，模型输入取当前选择，未选择时拒绝。</summary>
public sealed class KuroeTaskStartTests
{
    private static readonly PortName Model = new("MODEL_NAME");

    [Fact]
    public void Model_input_takes_the_current_selection()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
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
        using ModelServiceFixture fixture = ModelServiceFixture.Create(selectModel: false);

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
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
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = "NOPE" },
            new FlowName("默认"),
            "标题");

        Assert.True(values.IsError);
        Assert.Contains("未知的宿主开始输入 NOPE", values.FirstError.Description, StringComparison.Ordinal);
    }

    private static KuroeTaskStart Start(ModelServiceFixture fixture) =>
        new(Path.GetTempPath(), null, fixture.Models);
}