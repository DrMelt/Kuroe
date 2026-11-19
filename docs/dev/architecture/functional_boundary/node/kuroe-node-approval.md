# Kuroe.Node.Approval

审批节点类型：启动后等待批准，批准即按同名输出端口放出停驻取值，取消后不再接收外部输入。

## 职责

- 节点类型定义：类型名、静态形状与实例建立。
- 类型声明：等待批准时的提示文本。
- 参数声明：只声明提问文本，未知字段与形状在构造层报出。
- 停驻与放行：节点不启动执行，批准后按同名输出端口放出停驻时的取值。
- 注册入口：随宿主装配登记审批节点描述。

## 边界外

- 不含等待登记与产出：在 Kuroe.Runner.Approval 与 Kuroe.Input.Runtime。
- 不含图运行期与流程推进。
- 不含其他节点类型。

## 依赖

- Kuroe.Flow.Compilation.Shared、Kuroe.Runner.Approval、Kuroe.Tasks.Shared、Kuroe.Flow.Graph.Shared、Kuroe.Execution.Shared。