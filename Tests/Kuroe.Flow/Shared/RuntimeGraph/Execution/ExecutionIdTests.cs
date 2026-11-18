using Xunit;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行号的展示文本。</summary>
public sealed class ExecutionIdTests
{
    [Fact(DisplayName = "执行号渲染为可读文本")]
    public void Id_renders_for_the_human_reader() =>
        Assert.Equal("执行 #7", new ExecutionId(7).ToString());
}
