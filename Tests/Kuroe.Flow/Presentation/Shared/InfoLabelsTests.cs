using Kuroe.Flow.Execution;
using Kuroe.Flow.Presentation;
using Xunit;

namespace Kuroe.Flow.Presentation.Tests;

/// <summary>token 用量条目与请求次数的展示文本。</summary>
public sealed class InfoLabelsTests
{
    [Fact]
    public void Token_entries_group_input_and_output_with_their_subsets()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(100, 20, 120, 80, 5));

        Assert.Equal(
            [
                ("输入", "100（83%），缓存输入 80（占输入 80%）"),
                ("输出", "20（17%），推理 5（占输出 25%）"),
                ("合计", "120"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_omit_subsets_that_are_missing()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(100, 20, 120, null, null));

        Assert.Equal(
            [
                ("输入", "100（83%）"),
                ("输出", "20（17%）"),
                ("合计", "120"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_omit_total_shares_when_one_side_is_unknown()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(100, null, 100, null, null));

        Assert.Equal(
            [
                ("输入", "100"),
                ("输出", "—"),
                ("合计", "100"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_keep_a_subset_when_its_parent_is_unknown()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(null, 20, 20, 80, null));

        Assert.Equal(
            [
                ("输入", "—，缓存输入 80"),
                ("输出", "20"),
                ("合计", "20"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_keep_the_total_shares_at_one_hundred_after_rounding()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(1, 7, 8, null, null));

        Assert.Equal(
            [
                ("输入", "1（13%）"),
                ("输出", "7（87%）"),
                ("合计", "8"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_omit_shares_on_a_zero_base()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(0, 0, 0, 0, 0));

        Assert.Equal(
            [
                ("输入", "0，缓存输入 0"),
                ("输出", "0，推理 0"),
                ("合计", "0"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_omit_subset_share_when_the_subset_exceeds_its_parent()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(100, 20, 120, 120, 30));

        Assert.Equal(
            [
                ("输入", "100（83%），缓存输入 120"),
                ("输出", "20（17%），推理 30"),
                ("合计", "120"),
            ],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Token_entries_report_missing_when_no_counts_are_known()
    {
        ExecutionUsage usage = new(1, 0, new TokenUsage(null, null, null, 80, 5));

        Assert.Equal(
            [("用量", "未提供")],
            InfoLabels.TokenEntries(usage));
    }

    [Fact]
    public void Requests_append_the_count_of_requests_without_usage()
    {
        Assert.Equal("0 次", InfoLabels.Requests(ExecutionUsage.None));
        Assert.Equal("2 次（1 次未返回用量）", InfoLabels.Requests(new ExecutionUsage(2, 1, TokenUsage.None)));
        Assert.Equal("2 次（均未返回用量）", InfoLabels.Requests(new ExecutionUsage(2, 2, TokenUsage.None)));
    }
}