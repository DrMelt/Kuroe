using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>余下的契约模型：标识文本与通知构成。</summary>
public sealed class ContractModelTests
{
    [Fact]
    public void Ids_render_for_the_human_reader()
    {
        Assert.Equal("任务 00000003", new TaskId(3).ToString());
        Assert.Equal("执行 #7", new ExecutionId(7).ToString());
    }

    [Fact]
    public void ExecutionNotice_builds_from_errors()
    {
        IReadOnlyList<Error> errors = [Error.Failure("A", "甲失败"), Error.Failure("B", "乙失败")];

        ExecutionNotice notice = ExecutionNotice.From(errors, "启动失败");

        Assert.Equal(NoticeLevel.Error, notice.Level);
        Assert.Equal("启动失败：甲失败；乙失败", notice.Text);
    }
}
