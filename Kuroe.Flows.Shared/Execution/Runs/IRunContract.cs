namespace Kuroe.Flows.Shared.Execution.Runs;

/// <summary>本 run 的产出契约：该交回的内容还缺什么。执行器据它决定是否补一轮请求。</summary>
public interface IRunContract
{
    /// <summary>还缺交回内容时给出写给模型的补交要求，齐备时为空。</summary>
    string? Reminder(IRun run);
}
