# Kuroe.Maf.Compilation

MAF 文档装配实现：把描述文档经图流程编译投影成 MAF 流程定义，并提供按流程名取定义的来源。

## 职责

- 文档装配：描述文档交给流程描述编译入口，逐流程投影，错误一次给全。
- 流程投影：把图流程的开始端口、步骤、步骤输入与连接转成 MAF 流程定义。
- 来源解析：按节点作用域解析上游来源与静态输入。
- 文档来源：装配一次后按流程名提供定义。

## 边界外

- 不含装配契约：在 Kuroe.Maf.Compilation.Shared。
- 不含描述文档模型：在 Kuroe.Document.Shared。
- 不含图编译与图模型：在 Kuroe.Flow.Compilation 与 Kuroe.Flow.Graph.Shared。
- 不含流程定义与运行期：在 Kuroe.Maf.Definition 与 Kuroe.Maf.Control。

## 依赖

- Kuroe.Maf.Compilation.Shared、Kuroe.Maf.Definition、Kuroe.Document.Shared、Kuroe.Flow.Compilation.Shared、Kuroe.Flow.Graph.Shared、Kuroe.Tasks.Shared、Kuroe.Execution.Shared。