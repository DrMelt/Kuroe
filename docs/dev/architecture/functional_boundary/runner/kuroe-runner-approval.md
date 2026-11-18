# Kuroe.Runner.Approval

审批等待运行器：登记等待用户批准，收到批准后按停驻输入产出由节点输出投影发布。

## 职责

- 执行参数：等待批准时的提示文本与等待步骤键。
- 等待登记：按等待步骤键向输入通道登记。
- 产出收口：收到批准后按停驻输入的产出方式交回。

## 边界外

- 不含节点类型与参数读取：在 Kuroe.Node.Approval。
- 不含输入通道实现与流程推进。
- 不含图与任务域。

## 依赖

- Kuroe.Execution.Shared、Kuroe.Input.Shared、Kuroe.Tasks.Shared。