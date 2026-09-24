using ErrorOr;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Configuration;
using Kuroe.Shared.Workflows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>余下的契约模型：标识文本、影响标记与通知构成。</summary>
public sealed class ContractModelTests
{
    [Fact]
    public void Ids_render_for_the_human_reader()
    {
        Assert.Equal("任务 #3", new TaskId(3).ToString());
        Assert.Equal("agent #7", new RunId(7).ToString());
    }

    [Fact]
    public void SettingsEffect_none_means_no_change()
    {
        Assert.False(SettingsEffect.None.RequiresRestart);
        Assert.False(SettingsEffect.None.InvalidatesHistory);
    }

    [Fact]
    public void ExecutionNotice_builds_from_errors()
    {
        IReadOnlyList<Error> errors = [Error.Failure("A", "甲失败"), Error.Failure("B", "乙失败")];

        ExecutionNotice notice = ExecutionNotice.From(errors, "启动失败");

        Assert.Equal(NoticeLevel.Error, notice.Level);
        Assert.Equal("启动失败：甲失败；乙失败", notice.Text);
    }

    [Fact]
    public void DialogueReply_carries_its_discarded_flag()
    {
        DialogueReply reply = new("好的", true);

        Assert.Equal("好的", reply.Text);
        Assert.True(reply.Discarded);
    }
}