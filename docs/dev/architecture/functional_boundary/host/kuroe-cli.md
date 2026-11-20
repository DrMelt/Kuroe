# Kuroe.Cli

命令行入口：一次性命令，进程退出即结束。装配一次会话、执行一个操作、释放会话并给出退出码。

## 职责

- 命令树：全局工作目录选项递归到全部子命令。
- 服务装配与释放：装配核心服务，任务写存档并占用租约，启动时不加载存档，日志全部导向标准错误。
- 任务执行：提交任务并在本进程内驱动到完成、阻塞或取消，不接受等待外部输入的流程。
- 命令族：目录列出、导出与合并导入；配置打印、写入与删除用户层设置；流程列出、查看与导入；模型查看、切换、注册与注销；提供商列出、新增、更换凭据与删除。
- 输出分流：标准输出承载模型文本与结果，标准错误承载进度、工具调用与错误，写入串行。
- 纯文本呈现：目录、流程、配置、任务与执行。
- 退出码：按执行结果给出。

## 边界外

- 不含核心装配与任务域：在 Kuroe 与 Kuroe.GraphFlow。
- 不含图运行期、图控制与图共享层。
- 不含交互界面与等待型节点交付。
- 不含执行实现与流程推进。

## 依赖

- Kuroe、Kuroe.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.GraphFlow、Kuroe.Flow.Library、Kuroe.Kernel.Tasks.Runtime、Kuroe.Node.Llm、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Presentation.Shared、Kuroe.Kernel.Tasks.Presentation.Shared。