using ErrorOr;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Maf.Definition;

/// <summary>MAF 步骤标识。步骤标识同时是执行目标标识与流程步骤路径。</summary>
public readonly record struct MafStepId(string Value)
{
    /// <summary>解析步骤标识，拒绝空、全空白或含 / 的文本。</summary>
    public static ErrorOr<MafStepId> Create(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Contains('/')
            ? [Error.Validation(FlowErrorCodes.FlowComposition, "步骤标识不能为空或含 /。")]
            : new MafStepId(value.Trim());

    /// <summary>对应的执行目标标识。</summary>
    public ExecutionTargetId ToExecutionTargetId() => new(Value);

    /// <summary>对应的流程步骤路径。</summary>
    public FlowStepPath ToPath() => FlowStepPath.Of(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;
}