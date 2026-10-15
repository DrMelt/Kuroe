using ApiHub.Shared.Models;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Flow.Node.Llm;

/// <summary>宿主对执行节点的声明：本节点执行时使用的模型。</summary>
public sealed record KuroeNodeDeclaration(ModelName Model) : INodeDeclaration;
