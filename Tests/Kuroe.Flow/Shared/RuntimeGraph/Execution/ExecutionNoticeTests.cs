using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行通知：从错误集合取级别与文本。</summary>
public sealed class ExecutionNoticeTests
{
    [Fact]
    public void ExecutionNotice_builds_from_errors()
    {
        IReadOnlyList<Error> errors = [Error.Failure("A", "甲失败"), Error.Failure("B", "乙失败")];

        ExecutionNotice notice = ExecutionNotice.From(errors, "启动失败");

        Assert.Equal(NoticeLevel.Error, notice.Level);
        Assert.Equal("启动失败：甲失败；乙失败", notice.Text);
    }
}
