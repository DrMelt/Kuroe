using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Compilation.Shared;

/// <summary>一次文档编译的结果：全部流程已校验并编译。</summary>
/// <param name="flows">编译完成的流程，按文档声明序。</param>
public sealed class CompiledDocument(IReadOnlyList<CompiledFlow> flows)
{
    /// <summary>编译完成的流程，按文档声明序。</summary>
    public IReadOnlyList<CompiledFlow> Flows { get; } = flows;
}