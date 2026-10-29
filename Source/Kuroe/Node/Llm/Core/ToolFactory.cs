using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>按一次执行创建工具载体。每个 Llm 执行得到独立的工具集合。</summary>
public interface IToolFactory
{
    /// <summary>为一次执行创建全部工具载体。</summary>
    IReadOnlyList<ITool> Create(ExecutionId execution, ExecutionInput input);
}