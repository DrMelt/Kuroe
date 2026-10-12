using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一条图依赖：来源节点序号、来源输出端口、目标输入端口与类型，以及所属启动组序号。
/// 类型取自目标输入端口的声明，决定来源产出如何进入目标与是否参与启动；
/// 组是一套完整的输入绑定方案，组间任一齐备即启动。</summary>
public readonly record struct Dependency(int Source, PortName SourcePort, PortName TargetInput, PortType Type, int Group);
