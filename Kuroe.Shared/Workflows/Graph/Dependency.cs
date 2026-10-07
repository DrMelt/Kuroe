using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条图依赖：来源执行节点序号与命名的输出端口，未带端口即取整份产出。</summary>
public readonly record struct Dependency(int From, PortName? Port);
