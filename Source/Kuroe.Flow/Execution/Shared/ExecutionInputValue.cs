namespace Kuroe.Flow.Execution;

/// <summary>一次执行在一个输入端口上取到的内容。来源描述与来源标识都是不透明字符串，由流程控制解释。</summary>
/// <param name="Port">输入端口。</param>
/// <param name="Text">取到的内容。</param>
/// <param name="Source">来源描述，用于展示。</param>
/// <param name="SourceKey">执行记录使用的来源标识，为空时使用来源描述。</param>
public sealed record ExecutionInputValue(
    ExecutionPortName Port,
    string Text,
    string? Source,
    string? SourceKey = null);