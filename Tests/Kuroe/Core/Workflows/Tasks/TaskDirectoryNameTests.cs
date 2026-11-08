using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests.Workflows.Tasks;

/// <summary>任务目录名的拼装：任务键、流程与标题连接，非法字符替换与按字符截断。</summary>
public sealed class TaskDirectoryNameTests
{
    [Fact]
    public void Name_joins_task_key_flow_and_title()
    {
        TaskId id = new(3);

        string name = TaskDirectoryName.Of(id, new FlowName("审阅"), "修复登录超时");

        Assert.Equal($"{id.Value}-审阅-修复登录超时", name);
    }

    [Fact]
    public void Invalid_characters_are_replaced_and_the_flow_segment_keeps_no_hyphen()
    {
        TaskId id = new(7);

        string name = TaskDirectoryName.Of(id, new FlowName("合/同-审阅"), "修复: 登录?");

        Assert.Equal($"{id.Value}-合_同_审阅-修复_ 登录", name);
    }

    [Fact]
    public void Title_segment_keeps_hyphens()
    {
        TaskId id = new(1);

        string name = TaskDirectoryName.Of(id, new FlowName("默认"), "修复-a-b");

        Assert.Equal($"{id.Value}-默认-修复-a-b", name);
    }

    [Fact]
    public void Segments_are_truncated_by_character_without_splitting_surrogates()
    {
        string flow = new('流', 25);
        string title = new string('目', 39) + "😀" + new string('后', 5);

        TaskId id = new(1);

        string name = TaskDirectoryName.Of(id, new FlowName(flow), title);

        Assert.Equal($"{id.Value}-{new string('流', 20)}-{new string('目', 39)}😀", name);
    }
}