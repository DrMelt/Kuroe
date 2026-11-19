# Kuroe.Maf.Compilation.Shared

MAF 文档装配的共享契约：把描述文档装配成 MAF 流程定义的入口。

## 职责

- 文档装配入口：接收描述文档与内置节点定义，返回装配好的 MAF 流程定义。
- 装配结果：全部流程，或按流程给出的全部错误。

## 边界外

- 不含装配实现与投影：在 Kuroe.Maf.Compilation。
- 不含描述文档模型：在 Kuroe.Document.Shared。
- 不含图编译：在 Kuroe.Flow.Compilation。
- 不含流程定义与运行期：在 Kuroe.Maf.Definition 与 Kuroe.Maf.Control。

## 依赖

- Kuroe.Maf.Definition、Kuroe.Document.Shared、Kuroe.Flow.Graph.Shared、Kuroe.Tasks.Shared。