

namespace Kuroe.Flow.Graph.Shared.StaticGraph;

/// <summary>拉入绑定的消费键：来源节点与来源端口的组合。</summary>
public static class InputBindingExtensions
{
    /// <summary>已消费版本的键。</summary>
    public static ConsumptionKey Key(this InputBinding binding) => new(binding.Source.Node, binding.Source.Port);
}
