using Kuroe.Tool;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Llm;

/// <summary>按一次执行创建工具载体。每个 Llm 执行得到独立的工具集合。</summary>
public interface IToolFactory
{
    /// <summary>为一次执行创建全部工具载体。</summary>
    IReadOnlyList<ITool> Create(
        IExecutionRequestEntry requests,
        LlmExecutionParameters parameters);
}