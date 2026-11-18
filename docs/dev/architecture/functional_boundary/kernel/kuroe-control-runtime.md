# Kuroe.Control.Runtime

流程控制的运行期实现：活跃控制实例的维护与命令转发、执行索引、执行网关与流程模板目录。流程语义留在控制实例内，本层不解释流程种类。

## 职责

- 流程运行期：按完成信号维护活跃控制实例索引，转发启动、暂停、恢复、重新执行、终止与收口，退出时统一收口。
- 执行索引：执行号在全部任务域间递增分配，登记执行归属任务，按号取执行时一并交出归属。
- 执行网关：把执行号归属交给任务域后装配执行，其余执行操作转发给执行单元。
- 流程模板目录：聚合各流程实现提供的模板来源，供宿主浏览与生成提交表单。

## 边界外

- 不含控制契约：控制实例、网关与任务能力面的接口在 Kuroe.Control.Shared。
- 不含任务域：任务聚合、注册表与存档由 Kuroe.Tasks.Runtime 承担。
- 不含具体流程实现，不依赖图、节点与 MAF。

## 依赖

- Kuroe.Control.Shared、Kuroe.Execution.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Presentation.Shared、Kuroe.Presentation.Shared。