# Kuroe.Node.Llm

Llm 节点类型：由模型执行并交回端口产出。类型声明对本次执行的额外要求、能力白名单与恒定系统指令，运行期对象由引擎按形状建立。

## 职责

- 节点类型定义：类型名、静态形状与实例建立。
- 类型声明：对本次执行的额外要求、工具能力白名单与恒定系统指令块。
- 执行节点声明：给出本节点执行时使用的模型，供宿主解析。
- 工具路径规则：路径映射必须落在白名单内，并覆盖每个可用工具的全部变量。
- 注册入口：随宿主装配登记 Llm 节点描述。
- 无工具契约的目录：供不经过工具装配的流程编译与测试使用。

## 边界外

- 不含执行实现与会话：在 Kuroe.Runner.Llm 与 Kuroe.Runner.Shared。
- 不含工具实现与工具装配：在 Kuroe.Tool 与宿主。
- 不含图模型声明与图执行装配：在 Kuroe.Flow.NodeExecution。
- 不含图运行期与流程控制。

## 依赖

- Kuroe.Tool、Kuroe.Runner.Shared、Kuroe.Runner.Llm、Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.Shared、Kuroe.Tasks.Shared、Kuroe.Execution.Shared。
