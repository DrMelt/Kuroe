# Kuroe.Node.Input

输入节点类型：启动后等待用户回答，回答即产出放行下游。

## 职责

- 节点类型定义：类型名、静态形状与实例建立。
- 类型声明：等待回答时的提示文本。
- 参数声明：只声明提问文本，未知字段与形状在构造层报出。
- 等待语义：节点不启动执行，回答作为端口输出。
- 注册入口：随宿主装配登记输入节点描述。

## 边界外

- 不含等待登记与交付：在 Kuroe.Runner.Input 与 Kuroe.Input.Runtime。
- 不含执行装配与输出投影：在 Kuroe.Flow.NodeExecution。
- 不含图运行期与流程推进。
- 不含其他节点类型。

## 依赖

- Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared。