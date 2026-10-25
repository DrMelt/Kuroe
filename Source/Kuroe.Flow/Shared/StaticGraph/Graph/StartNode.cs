using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程的唯一来源节点：不属于任何容器，任务启动时即已放行。
/// 产出是提交任务时按端口给出的输入，节点经 From 引用 Start@端口 取用，容器外也能引用。
/// 除作者声明的端口外，引擎固定提供 WORKSPACE_PATH 与 TASK_WORK_PATH 两个端口，值在任务启动时给定。</summary>
public sealed record StartNode : GraphNode
{
    /// <summary>保留的节点名，节点库定义的名字不得与之相同。</summary>
    public static NodeName ReservedName { get; } = new("Start");

    /// <summary>保留的节点标签，流程内的节点标签不得与之相同。</summary>
    public static NodeLabel ReservedLabel { get; } = new(ReservedName.Value);

    /// <summary>WORKSPACE_PATH 固定端口。</summary>
    public static StartFixedPort WorkspacePath { get; } = new(
        new Port(new PortName("WORKSPACE_PATH"), PortTypes.Text, "工作区根目录，启动参数未给定时用进程当前目录。"),
        StartPathKind.Workspace);

    /// <summary>TASK_WORK_PATH 固定端口。</summary>
    public static StartFixedPort TaskWorkPath { get; } = new(
        new Port(new PortName("TASK_WORK_PATH"), PortTypes.Text, "本次任务的工作目录。"),
        StartPathKind.Task);

    /// <summary>引擎固定提供的开始端口，不属于流程声明。</summary>
    public static IReadOnlyList<StartFixedPort> FixedPorts { get; } = [WorkspacePath, TaskWorkPath];

    /// <summary>输出端口表：至少一个 Text 端口，不带值的端口至少一个，输出提交时按该端口给出的输入，其余端口带固定值。</summary>
    public required IReadOnlyList<Port> Outputs { get; init; }

    /// <summary>开始节点对外提供的全部端口：作者声明端口在前，引擎固定端口在后。</summary>
    public IReadOnlyList<Port> AllPorts => [.. Outputs, .. FixedPorts.Select(port => port.Port)];

    /// <summary>按名字取端口，作者声明优先，未声明时查引擎固定端口。</summary>
    public Port? FindPort(PortName name) => FindDeclared(name) ?? FindFixed(name)?.Port;

    /// <summary>按名字取作者声明的端口，未声明时为空。</summary>
    public Port? FindDeclared(PortName name) => Outputs.Find(name);

    /// <summary>按名字取引擎固定的端口，未声明时为空。</summary>
    public static StartFixedPort? FindFixed(PortName name)
    {
        foreach (StartFixedPort port in FixedPorts)
        {
            if (port.Port.Name == name)
            {
                return port;
            }
        }

        return null;
    }
}
