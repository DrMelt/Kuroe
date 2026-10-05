using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests;

/// <summary>系统时间工具：返回当地时间并附带 UTC 偏移。</summary>
public sealed class TimeToolTests
{
    [Fact]
    public void GetLocalTime_output_carries_utc_offset()
    {
        TimeTool tool = new();

        string output = tool.Functions.Single().Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.StartsWith("当前系统时间是：", output);
        Assert.Contains($"时区 UTC{DateTimeOffset.Now:zzz}", output);
    }
}