using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>宿主固定开始端口给出的路径种类。</summary>
public enum StartPathKind
{
    /// <summary>工作区根目录。</summary>
    Workspace,

    /// <summary>任务目录。</summary>
    Task,
}

/// <summary>宿主固定开始端口：端口声明与路径种类。固定端口由引擎提供，不写进流程文件。</summary>
/// <param name="Port">端口声明。</param>
/// <param name="Kind">取值的路径种类。</param>
public readonly record struct StartFixedPort(Port Port, StartPathKind Kind);