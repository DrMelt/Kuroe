# Kuroe.Maf.Compilation

MAF 文档装配实现：把流程描述经共享描述编译投影成 MAF 流程定义，并提供按流程名取定义的来源。

## 职责

- 文档装配：流程描述交给流程描述编译入口，逐流程投影，错误一次给全。
- 流程投影：把共享编译结果的开始端口、步骤、步骤输入与连接转成 MAF 流程定义。
- 来源解析：按节点作用域解析上游来源与静态输入。
- 文档来源：装配一次后按流程名提供定义。

## 边界外

- 不含装配契约：在 Kuroe.Maf.Compilation.Shared。
- 不含流程描述模型：在 Kuroe.FlowDescription.Shared。
- 不含静态模型与图编译：在 Kuroe.FlowDescription.Shared 与 Kuroe.FlowDescription.Compilation。
- 不含流程定义与运行期：在 Kuroe.Maf.Definition 与 Kuroe.Maf.Control。

## 依赖

- Kuroe.Maf.Compilation.Shared、Kuroe.Maf.Definition、Kuroe.FlowDescription.Shared、Kuroe.Tasks.Shared、Kuroe.Execution.Shared。