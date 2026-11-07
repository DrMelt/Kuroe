using ErrorOr;
using Xunit;

namespace Kuroe.Tool.Time.Tests;

/// <summary>系统时间工具：返回当地时间并附带 UTC 偏移。</summary>
public sealed class TimeToolTests
{
    [Fact]
    public void GetLocalTime_output_carries_utc_offset()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("UTC+08", TimeSpan.FromHours(8), "UTC+08", "UTC+08");
        TimeTool tool = new(new FixedClock(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)), zone));

        ErrorOr<string> output = tool.Functions.Single().Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.Equal("当前系统时间是：2026-01-02 03:04:05，时区 UTC+08:00", output.Value);
    }

    /// <summary>固定时刻与时区的时钟。</summary>
    private sealed class FixedClock(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => localTimeZone;
    }
}
