namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>宿主对执行节点的声明：执行目标、能力与措辞等宿主语义的载体。引擎只负责透传，不解释其中内容。</summary>
public interface INodeDeclaration
{
    /// <summary>执行使用的模型名，未声明时为空。</summary>
    string? Model => null;
}
