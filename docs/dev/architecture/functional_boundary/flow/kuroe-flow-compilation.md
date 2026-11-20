# Kuroe.Flow.Compilation

图流程编译：把展开后的流程定义编译成静态执行视图，并校验图级规则。

## 职责

- 图编译入口：`IFlowGraphCompiler` 与 `FlowGraphCompiler`。
- 静态执行视图：由展开流程建立 `NodeGraph`、`ExecutableNode` 与 `ContainerNode`。
- 图级规则：启动组、依赖、静态来源、输出绑定与容器作用域。
- 单条流程编译：`FlowCompiler` 串起语言规则、图编译与图规则。

## 边界外

- 不含描述模型与库展开：在 Kuroe.FlowDescription.Shared 与 Kuroe.FlowDescription.Compilation。
- 不含运行期与控制：在 Kuroe.Flow.Control。
- 不含 MAF 装配。

## 依赖

- Kuroe.FlowDescription.Shared、Kuroe.FlowDescription.Compilation、Kuroe.Flow.Graph.Shared、Kuroe.Tasks.Shared。
