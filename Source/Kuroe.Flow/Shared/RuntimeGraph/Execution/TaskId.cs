using System.Globalization;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>任务标识：跨进程唯一的 32 位十六进制键。界面显示末尾 8 位，存储与引用使用完整键。</summary>
public readonly record struct TaskId(string Value)
{
    /// <summary>完整键的字符数。</summary>
    private const int KeyLength = 32;

    /// <summary>显示用短键的字符数。</summary>
    private const int ShortLength = 8;

    /// <summary>以固定值构造键，供测试与稳定数据使用。运行期分配用 <see cref="New"/>。</summary>
    public TaskId(int value) : this(value.ToString("x32", CultureInfo.InvariantCulture))
    {
    }

    /// <summary>生成新键，同一进程与多个进程间都不重复。</summary>
    public static TaskId New() => new(Guid.CreateVersion7().ToString("N"));

    /// <summary>解析完整键，格式不符时返回 false。</summary>
    public static bool TryParse(string? text, out TaskId id)
    {
        if (text is { Length: KeyLength } && Guid.TryParseExact(text, "N", out _))
        {
            id = new TaskId(text);

            return true;
        }

        id = default;

        return false;
    }

    /// <summary>界面与错误消息使用的短键。</summary>
    public string Short => Value is { Length: > ShortLength } text ? text[^ShortLength..] : Value;

    /// <summary>按完整键的末尾段匹配短键引用。</summary>
    public bool MatchesShort(string reference) =>
        reference.Length > 0 && Value is { } key && key.EndsWith(reference, StringComparison.OrdinalIgnoreCase);

    /// <summary>任务标识的文本形式。</summary>
    public override string ToString() => $"任务 {Short}";
}
