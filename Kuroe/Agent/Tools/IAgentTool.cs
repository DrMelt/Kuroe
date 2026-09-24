namespace Kuroe.Agent.Tools;

/// <summary>工具载体。返回的声明是模型可调用的全部能力。</summary>
public interface IAgentTool
{
    /// <summary>本载体的函数声明。</summary>
    IReadOnlyList<ToolFunction> Functions { get; }
}
