using ErrorOr;

namespace Kuroe.Ability.Runner.Shared;

/// <summary>模型侧的错误构造。</summary>
public static class ModelErrors
{
    /// <summary>未选择模型。</summary>
    public static Error ModelNotSelected() => Error.Validation("Runtime.Model", "当前未选择模型。");
}