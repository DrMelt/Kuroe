using Kuroe.Flow.Presentation;
using Xunit;

namespace Kuroe.Flow.Presentation.Shared.Tests;

/// <summary>流程视图的顺位查询与步骤内容。</summary>
public sealed class FlowViewTests
{
    [Fact]
    public void IndexOf_returns_step_position()
    {
        FlowView view = new(
            "demo",
            "graph",
            null,
            [],
            [
                Step("start"),
                Step("finish"),
            ],
            []);

        Assert.Equal(0, view.IndexOf("start"));
        Assert.Equal(1, view.IndexOf("finish"));
        Assert.Null(view.IndexOf("missing"));
    }

    private static FlowStepView Step(string path) =>
        new(path, path, "test", "pending", [], [], [], 1);
}