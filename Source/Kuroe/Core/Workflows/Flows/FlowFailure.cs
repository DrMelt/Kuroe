using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Workflows.Flows;

/// <summary>一条不可用流程：流程名与编译错误。</summary>
public sealed record FlowFailure(FlowName Name, IReadOnlyList<Error> Errors);