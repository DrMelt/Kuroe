using ErrorOr;
using Kuroe.Agent;
using Kuroe.Configuration;

namespace Kuroe.Cli;

/// <summary>命令结果的终端呈现。</summary>
internal static class ConsoleResults
{
    /// <summary>成功时打印完成提示，失败时打印错误与未生效提示。</summary>
    public static void Report(ErrorOr<Success> result, string done)
    {
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        Console.WriteLine(done);
    }

    /// <summary>执行设置改动，成功后按改动的影响给出提示。</summary>
    public static void Apply(SettingsProvider settings, Func<ErrorOr<Success>> write)
    {
        AgentSettings before = settings.Current.Agent;

        ErrorOr<Success> result = write();
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        AgentSettings current = settings.Current.Agent;

        Console.WriteLine(current.RequiresRestart(before) ? "已保存，重启后生效。" : "已保存并生效。");
        if (current.InvalidatesHistory(before))
        {
            Console.WriteLine("下一轮对话将清空上下文。");
        }
    }

    /// <summary>打印错误并说明改动未生效。</summary>
    public static void Reject(IEnumerable<Error> errors)
    {
        ConsoleErrors.Report(errors);
        Console.WriteLine("未生效。");
    }

    /// <summary>收集解析失败的错误。</summary>
    public static void Collect<T>(ErrorOr<T> parsed, List<Error> errors)
    {
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);
        }
    }

    /// <summary>终端显示宽度，东亚宽字符占两列。</summary>
    public static int Width(string text) => text.Sum(character => IsWide(character) ? 2 : 1);

    /// <summary>右补齐到指定的显示宽度。</summary>
    public static string PadTo(string text, int width) =>
        text.PadRight(text.Length + Math.Max(0, width - Width(text)));

    private static bool IsWide(char character) =>
        character is (>= '\u1100' and <= '\u115f')
            or '\u2329'
            or '\u232a'
            or (>= '\u2e80' and <= '\ua4cf')
            or (>= '\uac00' and <= '\ud7a3')
            or (>= '\uf900' and <= '\ufaff')
            or (>= '\ufe30' and <= '\ufe6f')
            or (>= '\uff00' and <= '\uff60')
            or (>= '\uffe0' and <= '\uffe6');
}