using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>输入取值出处的持久形态转换。</summary>
public static class CheckpointSources
{
    /// <summary>把运行期出处写成持久形态。</summary>
    public static ContextSourceCheckpoint Write(ContextSource source) =>
        source switch
        {
            ExecutionSource execution => new ContextSourceCheckpoint(
                ContextSourceKind.Execution,
                execution.Execution,
                execution.Node,
                execution.Port),
            InputSource input => new ContextSourceCheckpoint(ContextSourceKind.Input, null, input.Node, input.Port),
            StartSource start => new ContextSourceCheckpoint(ContextSourceKind.Start, null, null, start.Port),
            ContainerPortSource container => new ContextSourceCheckpoint(
                ContextSourceKind.ContainerPort,
                null,
                container.Container,
                container.Port),
            DefaultSource value => new ContextSourceCheckpoint(ContextSourceKind.Default, null, value.Node, value.Port),
            ConstantSource value => new ContextSourceCheckpoint(ContextSourceKind.Constant, null, value.Node, value.Port),
            NodeSource node => new ContextSourceCheckpoint(ContextSourceKind.Node, null, node.Node, node.Port),
            _ => throw new ArgumentException($"未知的输入取值出处 {source.GetType().Name}。"),
        };

    /// <summary>从持久形态读取出处。</summary>
    public static ContextSource Read(ContextSourceCheckpoint source) =>
        source.Kind switch
        {
            ContextSourceKind.Execution => new ExecutionSource(
                Required(source.Execution, "执行出处"),
                Required(source.Node, "执行出处节点"),
                Required(source.Port, "执行出处端口")),
            ContextSourceKind.Input => new InputSource(
                Required(source.Node, "输入出处节点"),
                Required(source.Port, "输入出处端口")),
            ContextSourceKind.Start => new StartSource(Required(source.Port, "开始出处端口")),
            ContextSourceKind.ContainerPort => new ContainerPortSource(
                Required(source.Node, "容器出处节点"),
                Required(source.Port, "容器出处端口")),
            ContextSourceKind.Default => new DefaultSource(
                Required(source.Node, "默认值出处节点"),
                Required(source.Port, "默认值出处端口")),
            ContextSourceKind.Constant => new ConstantSource(
                Required(source.Node, "常量出处节点"),
                Required(source.Port, "常量出处端口")),
            ContextSourceKind.Node => new NodeSource(
                Required(source.Node, "节点出处节点"),
                Required(source.Port, "节点出处端口")),
            _ => throw new ArgumentException($"未知的输入取值出处 {source.Kind}。"),
        };

    private static T Required<T>(T? value, string field) where T : struct =>
        value ?? throw new ArgumentException($"检查点缺少{field}。");
}
