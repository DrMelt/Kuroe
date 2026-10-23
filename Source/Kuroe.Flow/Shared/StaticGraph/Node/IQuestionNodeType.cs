namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>等待型节点类型向宿主暴露的提问文本，未声明时为空。</summary>
public interface IQuestionNodeType
{
    /// <summary>等待外部输入时给用户的提问，未声明时为空。</summary>
    string? Question { get; }
}