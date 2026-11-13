using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Node.StaticGraph.Nodes;

/// <summary>节点类型的输出端口声明方式：由流程声明、从输入派生，或由类型给出。</summary>
public abstract record OutputDeclaration
{
    /// <summary>端口由流程或节点库声明。</summary>
    public sealed record Declared : OutputDeclaration
    {
        /// <summary>声明模式的共享实例。</summary>
        public static Declared Instance { get; } = new();
    }

    /// <summary>对外端口取自身输入端口，只有不派发执行的类型成立。</summary>
    public sealed record DerivedFromInputs : OutputDeclaration
    {
        /// <summary>派生模式的共享实例。</summary>
        public static DerivedFromInputs Instance { get; } = new();
    }

    /// <summary>端口由类型给出，流程不声明 Outputs。</summary>
    public sealed record Given(IReadOnlyList<Port> Ports) : OutputDeclaration;
}