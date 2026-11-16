namespace Kuroe.Execution.Shared;

/// <summary>一条执行会话消息的角色。</summary>
public enum ExecutionMessageRole
{
    /// <summary>用户输入。</summary>
    User,

    /// <summary>助手回复。</summary>
    Assistant,

    /// <summary>系统消息。</summary>
    System,

    /// <summary>工具结果消息。</summary>
    Tool,

    /// <summary>其它角色。</summary>
    Other,
}

/// <summary>一条执行会话消息中内容的种类。</summary>
public enum ExecutionContentKind
{
    /// <summary>文本。</summary>
    Text,

    /// <summary>模型思考文本。</summary>
    Reasoning,

    /// <summary>工具调用。</summary>
    ToolCall,

    /// <summary>工具结果。</summary>
    ToolResult,

    /// <summary>其它内容。</summary>
    Other,
}

/// <summary>一条会话消息中的一段内容。展示层决定是否显示。</summary>
public sealed record ExecutionContentPart(ExecutionContentKind Kind, string Text);

/// <summary>一条会话消息。</summary>
public sealed record ExecutionMessage(
    ExecutionMessageRole Role,
    IReadOnlyList<ExecutionContentPart> Parts);

/// <summary>一次执行保存的会话历史。消息按原始顺序排列，内容不过滤。</summary>
public sealed record ExecutionConversation(IReadOnlyList<ExecutionMessage> Messages)
{
    /// <summary>没有可读历史。</summary>
    public static ExecutionConversation Empty { get; } = new([]);
}