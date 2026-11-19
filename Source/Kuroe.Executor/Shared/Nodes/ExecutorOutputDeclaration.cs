namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目的输出端口声明方式：由流程声明、从输入派生，或由条目给出。</summary>
public abstract record ExecutorOutputDeclaration
{
    /// <summary>端口由流程或执行器库声明。</summary>
    public sealed record Declared : ExecutorOutputDeclaration
    {
        /// <summary>声明模式的共享实例。</summary>
        public static Declared Instance { get; } = new();
    }

    /// <summary>对外端口取自身输入端口，只有不派发执行的条目成立。</summary>
    public sealed record DerivedFromInputs : ExecutorOutputDeclaration
    {
        /// <summary>派生模式的共享实例。</summary>
        public static DerivedFromInputs Instance { get; } = new();
    }

    /// <summary>端口由条目给出，流程不声明输出端口。</summary>
    public sealed record Given(IReadOnlyList<ExecutorOutputPort> Ports) : ExecutorOutputDeclaration;
}