namespace Kuroe.Agent;

/// <summary>上下文的角色。库自定义而非模型框架的类型，避免宿主编译面出现外部依赖。</summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
}
