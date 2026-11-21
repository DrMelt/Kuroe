using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Composition.Tests.Support;
using Kuroe.GraphFlow.HostInputs;
using Xunit;

namespace Kuroe.Composition.Tests.Workflows.Tasks;

/// <summary>宿主开始输入：模型输入取当前选择，未选择时拒绝。输入名是否受支持由宿主开始输入表判定。</summary>
public sealed class KuroeTaskStartTests
{
    private static readonly PortName Model = new("MODEL_NAME");

    [Fact(DisplayName = "模型输入采用当前选择")]
    public void Model_input_takes_the_current_selection()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = StartSources.Model });

        Assert.False(values.IsError, string.Join("；", values.ErrorsOrEmptyList.Select(error => error.Description)));
        Assert.Equal("fake", values.Value.Text(Model));
    }

    [Fact(DisplayName = "没有选择时模型输入被拒绝")]
    public void Model_input_without_a_selection_is_rejected()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create(selectModel: false);

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = StartSources.Model });

        Assert.True(values.IsError);
        Assert.Equal("当前未选择模型。", values.FirstError.Description);
    }

    [Fact(DisplayName = "未知的宿主输入名返回错误，不抛异常")]
    public void Unknown_host_input_is_rejected()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<IPortTexts> values = Start(fixture).Prepare(
            new TaskId(1),
            new Dictionary<PortName, string> { [Model] = "NOPE" });

        Assert.True(values.IsError);
        Assert.Contains("未知的宿主开始输入 NOPE", values.FirstError.Description, StringComparison.Ordinal);
    }

    private static KuroeTaskStart Start(ModelServiceFixture fixture) =>
        new(Path.GetTempPath(), null, fixture.Models);
}