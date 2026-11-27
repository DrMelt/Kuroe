namespace Kuroe.Kernel.Tasks.Shared;

/// <summary>流程实现标识。同一流程名由多个实现提供时，用它选择实现。</summary>
public readonly record struct FlowImplementation(string Value)
{
    /// <summary>流程实现的文本形式。</summary>
    public override string ToString() => Value;
}