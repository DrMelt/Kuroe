using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>一个内置助手会话的只读摘要。模型与语言在会话建立时固定。</summary>
public sealed record DialogueSessionInfo(
    TaskId Id,
    string Title,
    string Model,
    string Language,
    DialogueMode Mode,
    TaskState State,
    bool Busy,
    int Rounds,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);