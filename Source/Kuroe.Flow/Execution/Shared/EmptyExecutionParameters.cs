namespace Kuroe.Flow.Execution.Shared;

/// <summary>没有类型专属参数的目标使用的空参数。</summary>
public sealed class EmptyExecutionParameters : IExecutionParameters
{
    /// <summary>空参数单例。</summary>
    public static EmptyExecutionParameters Instance { get; } = new();

    private EmptyExecutionParameters()
    {
    }
}