# Kuroe.Console

交互终端入口：主菜单是唯一入口，助手、任务、流程、目录、设置与工具浏览都是菜单项；负责终端渲染、按键输入与等待型节点的交付。

## 职责

- 终端交互循环：主菜单驱动各页面，页面进入时独占终端，退出时冲刷排队通知。
- 页面：助手会话、任务列表与三级下钻、流程菜单与模板树、模型与提供商菜单、设置菜单、工具树。
- 呈现：固定结构的块、清单表与键值表，终端样式与布局计算集中在一处。
- 输入：单行、多行与带框键盘选择，凭据按掩码回显，确认动作默认停在保留现状的选项。
- 等待型节点的交付：把输入表与审批交给用户，回答或批准后交付并推进流程。
- 执行通知与错误呈现：后台执行的过程不进终端，只报问题与可恢复错误的操作指引。
- 启动横幅：模型、目录与流程规模，缺少提供商、模型或工具时给出处理指引。
- 追加 Console 特有的等待型节点类型。

## 边界外

- 不含核心装配与任务域。
- 不含图流程实现、图运行期与节点类型实现。
- 不含执行与流程推进：只提交、交付输入与读取快照。

## 依赖

- Kuroe、Kuroe.Assistant、Kuroe.Node.Llm、Kuroe.Runner.Llm、Kuroe.Node.Input、Kuroe.Node.Approval、Kuroe.Kernel.Input.Shared、Kuroe.Tool、Kuroe.Tool.Json、Kuroe.GraphFlow、Kuroe.Flow.Library、Kuroe.Flow.Graph.Shared、Kuroe.Flow.Control、Kuroe.Flow.Records.Shared、Kuroe.Kernel.Tasks.Runtime、Kuroe.Kernel.Tasks.Persistence.Restoring.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.Platform、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Presentation.Shared、Kuroe.Kernel.Tasks.Presentation.Shared。