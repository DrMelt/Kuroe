# Kuroe.Flow.NodeLibrary

节点库装配：合并内置定义与文件定义，校验节点库后逐流程展开，并约束容器作用域。

## 职责

- 库装配入口：合并内置与文件定义，校验后逐流程展开为具体流程。
- 节点库定义校验：结构合法、端口声明完整、子树自包含。
- 容器作用域规则：成员的上游来源只落在容器子树或容器声明的传入端口，容器外的节点与端口不可见。

## 边界外

- 不含描述模型与连接模型：在 Kuroe.Flow.Compilation.Shared。
- 不含图运行期、流程控制与文件格式。
- 不含容器节点类型的运行语义：在 Kuroe.Flow.NodeType.Container。

## 依赖

- Kuroe.Flow.Shared、Kuroe.Flow.Compilation.Shared、Kuroe.Tasks.Shared、Kuroe.Flow.Node.Shared。