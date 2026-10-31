using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行号的展示文本。</summary>
public sealed class ExecutionIdTests
{
    [Fact]
    public void Id_renders_for_the_human_reader() =>
        Assert.Equal("执行 #7", new ExecutionId(7).ToString());
}
