using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Documents.Shared;

/// <summary>节点描述：定义与引用共享的公共字段。</summary>
public abstract record NodeDocument
{
    /// <summary>上游 From 组：执行节点按组启动，容器只有一组作为传入端口接线。</summary>
    public IReadOnlyList<FromGroupDocument> From { get; init; } = [];

    /// <summary>静态输入绑定：输入端口到来源引用文本，来源识别与 From 使用同一套作用域规则。
    /// 动态输入一律写在 From，容器只有一组 From。</summary>
    public IReadOnlyDictionary<string, string>? Bindings { get; init; }

    /// <summary>产出模式：逐次发布或首次发布后冻结。</summary>
    public NodeEmission? Emission { get; init; }

    /// <summary>执行次数上限。</summary>
    public int? MaxExecutions { get; init; }
}