# Kuroe.Maf.Control

MAF 流程控制：把流程定义构造成工作流运行，由 MAF 运行期决定步骤推进，每一步对应一次 Kuroe 执行，并负责检查点、视图、存档捕获与提交恢复。

## 职责

- 流程控制实例：启动与恢复 MAF 运行，消费运行事件，按步骤建立执行，终止与重新执行。
- 工作流构造：步骤来自定义，连接来自定义，执行器标识取自步骤标识。
- 步骤执行桥接：一次步骤运行对应一次执行，上游消息转成执行输入，交回的端口文本作为消息交给下游。
- 检查点：把运行期检查点写入流程状态，写入受任务 Gate 与检查点冻结控制，索引留在内存。
- 运行事件：把 MAF 运行事件转成流程运行事件，执行器标识还原为步骤。
- 提交与恢复：校验开始输入、建立流程状态、从存档重建执行。
- 流程表现：步骤来自定义，状态与执行来自任务。
- 存档捕获：流程身份、定义摘要与运行期检查点。
- 模板来源：宿主定义的流程进入流程目录。
- 步骤运行面：向步骤给出停止推进信号与中断上报入口。

## 边界外

- 不含流程定义：在 Kuroe.Maf.Definition。
- 不含流程记录与编解码：在 Kuroe.Maf.Records.Shared 与 Kuroe.Maf.Persistence。
- 不含执行实现与任务域。
- 不依赖图、节点与 Kuroe.Flow 下任何项目。

## 依赖

- Kuroe.Kernel.Control.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Kernel.Presentation.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Maf.Records.Shared、Kuroe.Maf.Definition。