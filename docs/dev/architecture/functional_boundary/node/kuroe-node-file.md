# Kuroe.Node.File

文件节点类型：文件读取节点按路径变量解析路径并读取内容，文件写入节点把唯一的内容输入写入文件并回交写入内容。

## 职责

- 节点类型定义：读取与写入两个类型的类型名、静态形状与实例建立。
- 参数声明：只声明路径，未知字段与形状在构造层报出。
- 参数读取：路径必填，路径变量映射与解析边界交给运行期。
- 变量规则：变量与路径占位符一一对应，映射端口必须已声明且承载文本。
- 注册入口：随宿主装配登记文件节点描述。

## 边界外

- 不含文件读写实现：在 Kuroe.Runner.File 与 Kuroe.Files。
- 不含图运行期与流程控制。
- 不含其他节点类型。

## 依赖

- Kuroe.Flow.Compilation.Shared、Kuroe.Shared、Kuroe.Runner.File、Kuroe.Tasks.Shared、Kuroe.Flow.Graph.Shared、Kuroe.Executor.Shared、Kuroe.Execution.Shared。