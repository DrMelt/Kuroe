using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条图依赖：来源节点序号与命名的输出端口，来源可以是执行节点或容器，未带端口即取整份产出。</summary>
public readonly record struct Dependency(int From, PortName? Port);
