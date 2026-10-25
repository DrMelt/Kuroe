using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using StartNode = Kuroe.Flow.Shared.StaticGraph.Graph.StartNode;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>开始节点在任务内的运行时对象：任务启动即已放行，产出是提交时按端口给出的输入，带固定值的端口给配置里的值，
/// 宿主固定端口给任务启动路径。版本恒为 1，下游只消费一次。不派发执行、不等待、不取消。</summary>
internal sealed class RuntimeStart(StartNode node, StartInputs inputs, StartPaths paths) : NodeRuntime(node)
{
    /// <summary>提交时按端口给出的输入，不带固定值的端口的产出。</summary>
    private readonly StartInputs _inputs = inputs;

    /// <summary>任务启动时的宿主路径，引擎固定端口的产出。</summary>
    private readonly StartPaths _paths = paths;

    public override bool Released => true;

    public override long Revision => 1;

    public override bool Canceled => false;

    /// <summary>按端口取输入：宿主固定端口给任务启动路径，带固定值的端口给该值，其余 Text 端口给提交的输入，未声明端口无产出。</summary>
    public override string? PortText(INodeContext context, PortName port) => Text(port);

    /// <summary>按端口取初始输入消息，出处指向流程起点。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Text(port) is { Length: > 0 } text
            ? [new ContextValue(new StartSource(port), text)]
            : [];

    public override void Cancel()
    {
    }

    private string? Text(PortName port)
    {
        if (StartNode.FindFixed(port) is { } fixedPort)
        {
            return fixedPort.Kind switch
            {
                StartPathKind.Workspace => _paths.WorkspaceDirectory,
                StartPathKind.Task => _paths.TaskDirectory,
                _ => null,
            };
        }

        return node.FindDeclared(port) is { } declared && Ports.CarriesText(declared.Type)
            ? declared.Value ?? _inputs.Text(port)
            : null;
    }
}
