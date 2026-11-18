# Kuroe.Node.Command

命令节点类型：按命令变量展开执行目录与参数并执行一次命令，把合并输出发布到文本端口。

## 职责

- 节点类型定义：类型名、静态形状与实例建立。
- 参数声明：程序、参数、执行目录、超时与输出上限；未知字段与形状在构造层报出。
- 参数读取：程序必填，执行目录保留原文，命令变量与解析边界交给运行期。
- 变量规则：变量与执行目录和参数的占位符一一对应，映射端口必须已声明且承载文本。
- 注册入口：随宿主装配登记命令节点描述。

## 边界外

- 不含命令执行与进程能力：在 Kuroe.Runner.Command 与 Kuroe.Process。
- 不含图运行期与流程控制。
- 不含其他节点类型。

## 依赖

- Kuroe.Flow.Compilation.Shared、Kuroe.Shared、Kuroe.Runner.Command、Kuroe.Tasks.Shared、Kuroe.Flow.Node.Shared、Kuroe.Execution.Shared。