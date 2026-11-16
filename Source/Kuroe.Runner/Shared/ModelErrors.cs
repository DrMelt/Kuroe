using ErrorOr;

namespace Kuroe.Runner;

/// <summary>模型侧的错误构造。</summary>
public static class ModelErrors
{
    /// <summary>未选择模型，选择指引由调用方给出。</summary>
    public static Error ModelNotSelected() => Error.Validation("Runtime.Model", "当前未选择模型。");
}