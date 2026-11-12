using Kuroe.Flow.Execution;

namespace Kuroe.Node.Llm;

/// <summary>Llm 执行的类型专属参数：节点类型与本次执行使用的模型。</summary>
public sealed record LlmExecutionParameters(LlmNodeType Type, string? Model) : IExecutionParameters;