# Kuroe.Runner.Command

命令执行运行器：按命令变量展开执行目录与参数，执行一次命令并给出合并输出。

## 职责

- 执行参数：程序、参数、执行目录原文、路径变量映射、边界根、超时、输出上限与退出码策略。
- 变量展开：变量原文来自输入端口，路径结果必须落在工作区根内。
- 命令执行：调用进程能力执行一次命令，合并标准输出与标准错误，失败原因随执行交回。

## 边界外

- 不含节点类型与参数读取：在 Kuroe.Node.Command。
- 不含进程实现：在 Kuroe.Process。
- 不含执行状态机与任务域。

## 依赖

- Kuroe.Kernel.Execution.Shared、Kuroe.Process、Kuroe.Platform。