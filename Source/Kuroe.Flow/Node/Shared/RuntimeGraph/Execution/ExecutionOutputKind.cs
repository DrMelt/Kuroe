namespace Kuroe.Flow.Node.RuntimeGraph.Execution;

/// <summary>执行节点的产出形态。产出形态由类型声明，引擎按它选择发布方式。</summary>
public enum ExecutionOutputKind
{
    /// <summary>执行经命名端口交回产出，按端口取值发布。</summary>
    Ports,

    /// <summary>执行产出一段文本，按承载文本的输出端口发布。</summary>
    Content,
}