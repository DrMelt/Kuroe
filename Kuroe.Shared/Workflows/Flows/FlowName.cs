using ErrorOr;

namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程名。流程集合内唯一，展示即原文。</summary>
public readonly record struct FlowName(string Value)
{
    /// <summary>解析流程名，拒绝空或全空白文本，解析成功即非空白。</summary>
    public static ErrorOr<FlowName> Create(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? [Error.Validation(ErrorCodes.FlowName, "流程名不能为空。")]
            : new FlowName(name.Trim());

    /// <summary>流程名的文本形式。</summary>
    public override string ToString() => Value;
}
