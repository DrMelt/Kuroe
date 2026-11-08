namespace Kuroe.Tool;

/// <summary>一次工具调用的记录，参数文本由执行实参生成。</summary>
/// <param name="Name">工具路径，模型按它调用。</param>
/// <param name="Arguments">调用参数的 JSON 文本，无参时为空对象。</param>
/// <param name="Outcome">工具返回的原文，失败时为错误说明。</param>
/// <param name="Failed">该次调用是否失败。</param>
public sealed record ToolCallRecord(string Name, string Arguments, string Outcome, bool Failed);
